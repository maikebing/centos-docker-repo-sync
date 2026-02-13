#!/bin/bash
# Build and run the Docker container (C# version)

echo "构建 Docker 镜像（含 C# 同步工具）..."
docker build -t centos-docker-repo-sync .

echo ""
echo "用法:"
echo "  循环同步（默认，每24小时一次）:"
echo "    docker run -d -v ./repos:/data/repos centos-docker-repo-sync"
echo ""
echo "  只执行一次同步:"
echo "    docker run -it --rm -v ./repos:/data/repos centos-docker-repo-sync --once"
echo ""
echo "  使用 docker-compose:"
echo "    docker-compose up -d"
echo ""

# 默认交互运行（单次同步）
docker run -it --rm -v ./repos:/data/repos centos-docker-repo-sync --once