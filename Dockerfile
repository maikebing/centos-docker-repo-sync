FROM centos:7.9.2009

LABEL maintainer="maikebing"
LABEL description="CentOS 7.9.2009 and Docker CE repository sync container"
RUN sed -e "s|^mirrorlist=|#mirrorlist=|g" \
    -e "s|^#baseurl=http://mirror.centos.org/centos/\$releasever|baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009|g" \
    -e "s|^#baseurl=http://mirror.centos.org/\$contentdir/\$releasever|baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009|g" \
    -i.bak \
    /etc/yum.repos.d/CentOS-*.repo && \
    yum makecache && \
    yum install -y \
    createrepo \
    yum-utils \
    wget \
    rsync \
    && yum clean all

# 创建同步目录
RUN mkdir -p /data/repos/centos/7.9.2009 \
    && mkdir -p /data/repos/docker-ce

# 创建同步脚本
RUN cat > /usr/local/bin/sync-repos.sh << 'EOF'
#!/bin/bash

set -e

CENTOS_SOURCE="https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009"
CENTOS_TARGET="/data/repos/centos/7.9.2009"
DOCKER_TARGET="/data/repos/docker-ce"

echo "=========================================="
echo "开始同步仓库: $(date)"
echo "=========================================="

# 同步 CentOS 7.9.2009 仓库
echo "正在同步 CentOS 7.9.2009 仓库..."
mkdir -p ${CENTOS_TARGET}

# 定义需要同步的 CentOS 仓库
REPOS=("os" "updates" "extras" "centosplus")

for repo in "${REPOS[@]}"; do
    echo "同步 ${repo} 仓库..."
    mkdir -p ${CENTOS_TARGET}/${repo}
    
    # 使用 reposync 同步仓库
    reposync -c /etc/yum.repos.d/CentOS-Vault.repo \
        --repoid=C7.9.2009-${repo} \
        --download-metadata \
        --download_path=${CENTOS_TARGET}/${repo} \
        -n || echo "警告: ${repo} 同步可能不完整"
    
    # 创建仓库元数据
    if [ -d "${CENTOS_TARGET}/${repo}" ]; then
        echo "为 ${repo} 创建元数据..."
        createrepo --update ${CENTOS_TARGET}/${repo} || \
        createrepo ${CENTOS_TARGET}/${repo}
    fi
done

# 同步 Docker CE 仓库
echo "正在同步 Docker CE 仓库..."
mkdir -p ${DOCKER_TARGET}/centos/7/x86_64

# 添加 Docker CE 仓库
if [ ! -f /etc/yum.repos.d/docker-ce.repo ]; then
    yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
fi

# 同步 Docker CE stable 仓库
reposync --repoid=docker-ce-stable \
    --download-metadata \
    --download_path=${DOCKER_TARGET} \
    -n || echo "警告: Docker CE 同步可能不完整"

# 创建 Docker 仓库元数据
if [ -d "${DOCKER_TARGET}/docker-ce-stable" ]; then
    echo "为 Docker CE 创建元数据..."
    createrepo --update ${DOCKER_TARGET}/docker-ce-stable || \
    createrepo ${DOCKER_TARGET}/docker-ce-stable
fi

echo "=========================================="
echo "同步完成: $(date)"
echo "=========================================="
echo "CentOS 仓库位置: ${CENTOS_TARGET}"
echo "Docker 仓库位置: ${DOCKER_TARGET}"
echo "=========================================="

# 显示磁盘使用情况
du -sh ${CENTOS_TARGET} 2>/dev/null || echo "CentOS 目录为空"
du -sh ${DOCKER_TARGET} 2>/dev/null || echo "Docker 目录为空"
EOF

RUN chmod +x /usr/local/bin/sync-repos.sh

# 配置 CentOS Vault 仓库
RUN cat > /etc/yum.repos.d/CentOS-Vault.repo << 'EOF'
[C7.9.2009-base]
name=CentOS-7.9.2009 - Base
baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/os/x86_64/
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7
enabled=0

[C7.9.2009-updates]
name=CentOS-7.9.2009 - Updates
baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/updates/x86_64/
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7
enabled=0

[C7.9.2009-extras]
name=CentOS-7.9.2009 - Extras
baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/extras/x86_64/
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7
enabled=0

[C7.9.2009-centosplus]
name=CentOS-7.9.2009 - CentOSPlus
baseurl=https://mirrors.tuna.tsinghua.edu.cn/centos-vault/7.9.2009/centosplus/x86_64/
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7
enabled=0
EOF

# 设置工作目录
WORKDIR /data/repos

# 暴露数据卷
VOLUME ["/data/repos"]

# 默认命令
CMD ["/bin/bash", "-c", "/usr/local/bin/sync-repos.sh && tail -f /dev/null"]