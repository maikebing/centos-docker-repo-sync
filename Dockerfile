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

# 复制同步脚本
COPY sync-repos.sh /usr/local/bin/sync-repos.sh
RUN chmod +x /usr/local/bin/sync-repos.sh

# 配置 CentOS Vault 仓库
COPY CentOS-Vault.repo /etc/yum.repos.d/CentOS-Vault.repo

# 设置工作目录
WORKDIR /data/repos

# 暴露数据卷
VOLUME ["/data/repos"]

# 默认命令
CMD ["/usr/local/bin/sync-repos.sh"]