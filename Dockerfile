# ==========================================
# 阶段1: 使用 .NET SDK 编译 C# 同步工具
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# 先复制 csproj 还原依赖（利用 Docker 缓存层）
COPY RepoSync/RepoSync/RepoSync.csproj ./RepoSync/RepoSync/
RUN dotnet restore ./RepoSync/RepoSync/RepoSync.csproj

# 复制全部源码并发布
COPY RepoSync/RepoSync/ ./RepoSync/RepoSync/
RUN dotnet publish ./RepoSync/RepoSync/RepoSync.csproj \
    -c Release \
    -o /app \
    --self-contained false

# ==========================================
# 阶段2: 运行时镜像（不再需要 CentOS + yum）
# ==========================================
FROM mcr.microsoft.com/dotnet/runtime:10.0

LABEL maintainer="maikebing"
LABEL description="CentOS 7.9.2009 and Docker CE repository sync container (C# version)"

# 设置 UTF-8 编码环境，避免中文日志乱码
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8
ENV DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION=1

# 创建同步目录
RUN mkdir -p /data/repos/centos/7.9.2009 \
    && mkdir -p /data/repos/docker-ce \
    && mkdir -p /data/repos/epel/7

# 从构建阶段复制编译产物
COPY --from=build /app /app

# 设置工作目录
WORKDIR /data/repos

# 暴露数据卷
VOLUME ["/data/repos"]

# 默认命令：运行 C# 同步工具
ENTRYPOINT ["dotnet", "/app/RepoSync.dll"]