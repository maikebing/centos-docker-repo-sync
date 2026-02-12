# CentOS 7.9.2009 and Docker CE Repository Sync Container

This project contains a Dockerfile to build a CentOS 7.9.2009 based container that syncs the CentOS and Docker CE repositories.

## Prerequisites

- Docker (version 1.13 or higher)
- CentOS 7.9.2009 sources

## How to Build

1. Clone the repository:
   ```
   git clone https://github.com/maikebing/centos-docker-repo-sync.git
   cd centos-docker-repo-sync
   ```

2. Build the Docker image:
   ```
   docker build -t centos-docker-repo-sync .
   ```

## How to Run

To run the container:
```bash
docker run -it --rm centos-docker-repo-sync
```

## Customization

You can edit the `Dockerfile` to customize the behavior of the container.

## Author

- Maintainer: maikebing

## License

- This project is licensed under the MIT License.