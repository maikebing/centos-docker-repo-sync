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
CMD ["/usr/local/bin/sync-repos.sh"]