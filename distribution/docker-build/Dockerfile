FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

ENV DEBIAN_FRONTEND noninteractive 

RUN apt-get update && \
    apt-get -y -o Dpkg::Options::="--force-confold" install --no-install-recommends \
        apt-transport-https \
        wget dirmngr gpg gpg-agent \
        software-properties-common && \
    rm -rf /var/lib/apt/lists/*
	
RUN apt-get update && apt-get install -y \
        devscripts build-essential tofrodos \
        dh-make dh-systemd \
        cli-common-dev \
        sqlite3 libcurl4 mediainfo
RUN apt-get upgrade -y

COPY debian-start.sh /debian-start.sh
RUN  fromdos /debian-start.sh

WORKDIR /data
VOLUME [ "/data/controllarr_bin", "/data/build", "/data/output" ]
CMD /debian-start.sh
