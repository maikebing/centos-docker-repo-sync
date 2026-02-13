#!/bin/bash

# 日志函数
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $@"
}

# 检查远程 repomd.xml 是否有变化
# 参数: $1=远程 repomd URL, $2=本地 repomd 路径
check_repomd_changed() {
    local remote_url="$1"
    local local_file="$2"
    local tmp_file="/tmp/repomd_remote.xml"

    # 本地不存在，需要同步
    if [ ! -f "${local_file}" ]; then
        log "本地元数据不存在，需要同步"
        return 0
    fi

    # 下载远程 repomd.xml
    wget -q -O "${tmp_file}" "${remote_url}" 2>/dev/null
    if [ $? -ne 0 ]; then
        log "无法获取远程元数据，执行同步"
        rm -f "${tmp_file}"
        return 0
    fi

    # 对比 md5
    local remote_md5=$(md5sum "${tmp_file}" | awk '{print $1}')
    local local_md5=$(md5sum "${local_file}" | awk '{print $1}')
    rm -f "${tmp_file}"

    if [ "${remote_md5}" = "${local_md5}" ]; then
        return 1  # 未变化
    else
        return 0  # 有变化
    fi
}

CENTOS_SOURCE="https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009"
CENTOS_TARGET="/data/repos/centos/7.9.2009"
DOCKER_TARGET="/data/repos/docker-ce"
SYNC_INTERVAL=86400  # 每天执行一次（86400秒 = 24小时）

while true; do

log "=========================================="
log "开始同步仓库"
log "=========================================="

# 同步 CentOS 7.9.2009 仓库
log "正在同步 CentOS 7.9.2009 仓库..."
mkdir -p ${CENTOS_TARGET}

# 定义仓库名和对应的 repoid
declare -A REPO_MAP
REPO_MAP["os"]="C7.9.2009-base"
REPO_MAP["updates"]="C7.9.2009-updates"
REPO_MAP["extras"]="C7.9.2009-extras"

# 定义仓库对应的远程 baseurl
declare -A REPO_URL
REPO_URL["os"]="https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/os/x86_64"
REPO_URL["updates"]="https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/updates/x86_64"
REPO_URL["extras"]="https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/extras/x86_64"

for repo in "${!REPO_MAP[@]}"; do
    repoid="${REPO_MAP[$repo]}"
    target="${CENTOS_TARGET}/${repo}"
    remote_repomd="${REPO_URL[$repo]}/repodata/repomd.xml"
    local_repomd="${target}/repodata/repomd.xml"
    
    log "检查 ${repo} 仓库是否有更新..."
    if ! check_repomd_changed "${remote_repomd}" "${local_repomd}"; then
        log "${repo} 仓库未变化，跳过同步"
        continue
    fi
    
    log "开始同步 ${repo} 仓库 (repoid: ${repoid})..."
    
    # 使用 reposync 同步仓库，下载到 repoid 目录
    log "执行 reposync 命令下载 ${repo} 仓库..."
    reposync -c /etc/yum.repos.d/CentOS-Vault.repo \
        --repoid=${repoid} \
        --download-metadata \
        --download_path=${CENTOS_TARGET} \
        -n || log "警告: ${repo} 同步可能不完整"
    
    # 将 repoid 目录重命名为干净的目录名
    if [ -d "${CENTOS_TARGET}/${repoid}" ] && [ "${repoid}" != "${repo}" ]; then
        log "整理目录: ${repoid} -> ${repo}"
        # 如果目标目录已存在，先合并内容
        if [ -d "${target}" ]; then
            mv ${CENTOS_TARGET}/${repoid}/* ${target}/ 2>/dev/null || true
            rm -rf ${CENTOS_TARGET}/${repoid}
        else
            mv ${CENTOS_TARGET}/${repoid} ${target}
        fi
    fi
    
    # 创建仓库元数据
    if [ -d "${target}" ]; then
        log "为 ${repo} 创建元数据..."
        createrepo --update ${target} || \
        createrepo ${target}
        log "${repo} 元数据创建完成"
    fi
done

# 同步 Docker CE 仓库
DOCKER_REPOMD="https://download.docker.com/linux/centos/7/x86_64/stable/repodata/repomd.xml"
DOCKER_LOCAL_REPOMD="${DOCKER_TARGET}/docker-ce-stable/repodata/repomd.xml"

log "检查 Docker CE 仓库是否有更新..."
if check_repomd_changed "${DOCKER_REPOMD}" "${DOCKER_LOCAL_REPOMD}"; then
    log "正在同步 Docker CE 仓库..."
    mkdir -p ${DOCKER_TARGET}/centos/7/x86_64

# 添加 Docker CE 仓库
if [ ! -f /etc/yum.repos.d/docker-ce.repo ]; then
    log "添加 Docker CE 仓库..."
    yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
fi

# 同步 Docker CE stable 仓库
log "执行 reposync 命令下载 Docker CE 仓库..."
reposync --repoid=docker-ce-stable \
    --download-metadata \
    --download_path=${DOCKER_TARGET} \
    -n || log "警告: Docker CE 同步可能不完整"

# 创建 Docker 仓库元数据
if [ -d "${DOCKER_TARGET}/docker-ce-stable" ]; then
    log "为 Docker CE 创建元数据..."
    createrepo --update ${DOCKER_TARGET}/docker-ce-stable || \
    createrepo ${DOCKER_TARGET}/docker-ce-stable
    log "Docker CE 元数据创建完成"
fi
else
    log "Docker CE 仓库未变化，跳过同步"
fi

log "=========================================="
log "同步完成"
log "=========================================="
log "CentOS 仓库位置: ${CENTOS_TARGET}"
log "Docker 仓库位置: ${DOCKER_TARGET}"
log "=========================================="

# 同步 EPEL 仓库
EPEL_TARGET="/data/repos/epel/7"
EPEL_REPOMD="https://mirrors.aliyun.com/epel/7/x86_64/repodata/repomd.xml"
EPEL_LOCAL_REPOMD="${EPEL_TARGET}/epel/repodata/repomd.xml"

log "检查 EPEL 仓库是否有更新..."
if check_repomd_changed "${EPEL_REPOMD}" "${EPEL_LOCAL_REPOMD}"; then
    log "正在同步 EPEL 仓库..."
    mkdir -p ${EPEL_TARGET}
    reposync -c /etc/yum.repos.d/CentOS-Vault.repo \
        --repoid=epel \
        --download-metadata \
        --download_path=${EPEL_TARGET} \
        -n || log "警告: EPEL 同步可能不完整"

    if [ -d "${EPEL_TARGET}" ]; then
        log "为 EPEL 创建元数据..."
        createrepo --update ${EPEL_TARGET}/epel || \
        createrepo ${EPEL_TARGET}/epel
        log "EPEL 元数据创建完成"
    fi
else
    log "EPEL 仓库未变化，跳过同步"
fi

# 显示磁盘使用情况
log "CentOS 目录大小: $(du -sh ${CENTOS_TARGET} 2>/dev/null || echo "目录为空")"
log "Docker 目录大小: $(du -sh ${DOCKER_TARGET} 2>/dev/null || echo "目录为空")"
log "EPEL 目录大小: $(du -sh ${EPEL_TARGET} 2>/dev/null || echo "目录为空")"

log "下次同步将在 24 小时后执行..."
sleep ${SYNC_INTERVAL}

done
