
# CentOS-Docker-Repo-Sync

## 项目简介

本项目用于同步 CentOS 7.9.2009、Docker CE、EPEL 等 RPM 仓库，支持通过 Docker 容器自动化同步并生成元数据，方便本地搭建镜像源。核心同步逻辑由 C# 实现，替代传统 shell 脚本，提升并发性能和可维护性。

## 功能特性
- 支持同步 CentOS 7.9.2009、Docker CE、EPEL 仓库
- 自动检测 repomd.xml 变更，仅同步有更新的仓库
- 并发下载 RPM 包，自动生成仓库元数据（primary.xml.gz、repomd.xml 等）
- 支持本地缓存，避免重复下载
- 可通过 Docker 或 docker-compose 部署
- 提供 Nginx 配置，支持 HTTP 镜像服务

## 目录结构
- `repos/`：同步后的 RPM 仓库目录
- `RepoSync/`：C# 仓库同步工具源码
- `Dockerfile`：容器构建文件
- `docker-compose.yml`：一键部署配置
- `nginx.conf`：Nginx 镜像服务配置
- `build-and-run.sh`：快速构建与运行脚本

## 快速开始

### 1. 构建 Docker 镜像
```bash
git clone https://github.com/maikebing/centos-docker-repo-sync.git
cd centos-docker-repo-sync
docker build -t centos-docker-repo-sync .
```

### 2. 运行同步容器
- 循环同步（每24小时一次）：
   ```bash
   docker run -d -v ./repos:/data/repos centos-docker-repo-sync
   ```
- 只执行一次同步：
   ```bash
   docker run -it --rm -v ./repos:/data/repos centos-docker-repo-sync --once
   ```
- 使用 docker-compose：
   ```bash
   docker-compose up -d
   ```

### 3. 镜像服务
- 启动 Nginx 服务后，可通过 http://localhost:8080 访问同步后的仓库。

## 自定义与扩展
- 可修改 `Dockerfile`、`nginx.conf`、`RepoSync/RepoSync/SyncConfig.cs` 等文件调整同步源、同步频率、并发数等参数。

## 许可证
MIT License

---

# CentOS-Docker-Repo-Sync (English)

## Project Overview

This project syncs CentOS 7.9.2009, Docker CE, and EPEL RPM repositories, automating mirror setup via Docker containers. The core sync logic is implemented in C#, replacing shell scripts for better concurrency and maintainability.

## Features
- Sync CentOS 7.9.2009, Docker CE, and EPEL repositories
- Detect repomd.xml changes, sync only updated repos
- Concurrent RPM downloads, auto metadata generation (primary.xml.gz, repomd.xml, etc.)
- Local cache to avoid redundant downloads
- Deploy via Docker or docker-compose
- Nginx config for HTTP mirror service

## Directory Structure
- `repos/`: Synced RPM repository directory
- `RepoSync/`: C# sync tool source code
- `Dockerfile`: Container build file
- `docker-compose.yml`: One-click deployment config
- `nginx.conf`: Nginx mirror service config
- `build-and-run.sh`: Quick build & run script

## Quick Start

### 1. Build Docker Image
```bash
git clone https://github.com/maikebing/centos-docker-repo-sync.git
cd centos-docker-repo-sync
docker build -t centos-docker-repo-sync .
```

### 2. Run Sync Container
- Loop sync (every 24h):
   ```bash
   docker run -d -v ./repos:/data/repos centos-docker-repo-sync
   ```
- One-time sync:
   ```bash
   docker run -it --rm -v ./repos:/data/repos centos-docker-repo-sync --once
   ```
- Using docker-compose:
   ```bash
   docker-compose up -d
   ```

### 3. Mirror Service
- After Nginx starts, visit http://localhost:8080 to access the synced repo.

## Customization
- Edit `Dockerfile`, `nginx.conf`, or `RepoSync/RepoSync/SyncConfig.cs` to change sources, sync interval, concurrency, etc.

## License
MIT License