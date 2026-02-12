#!/bin/bash
# Build and run the Docker container

docker build -t centos-docker-repo-sync .
docker run -it --rm centos-docker-repo-sync