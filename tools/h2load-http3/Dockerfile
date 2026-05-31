# syntax=docker/dockerfile:1

ARG UBUNTU_VERSION=24.04
FROM ubuntu:${UBUNTU_VERSION} AS build

ARG AWS_LC_VERSION=v1.72.0
ARG NGHTTP3_VERSION=v1.15.0
ARG NGTCP2_VERSION=v1.22.1
ARG NGHTTP2_VERSION=v1.69.0

ENV DEBIAN_FRONTEND=noninteractive
ENV PREFIX=/opt/h2load-http3
ENV AWS_LC_PREFIX=/opt/aws-lc

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        autoconf \
        automake \
        bison \
        ca-certificates \
        cmake \
        g++ \
        git \
        libc-ares-dev \
        libev-dev \
        libtool \
        make \
        ninja-build \
        pkg-config \
        python3 \
        ruby \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

RUN git clone --depth 1 --branch ${AWS_LC_VERSION} https://github.com/aws/aws-lc.git \
    && cmake -S aws-lc -B aws-lc/build -G Ninja \
        -DCMAKE_BUILD_TYPE=Release \
        -DDISABLE_GO=ON \
        -DCMAKE_INSTALL_PREFIX=${AWS_LC_PREFIX} \
    && cmake --build aws-lc/build \
    && cmake --install aws-lc/build

RUN git clone --depth 1 --branch ${NGHTTP3_VERSION} https://github.com/ngtcp2/nghttp3.git \
    && cd nghttp3 \
    && git submodule update --init --depth 1 \
    && autoreconf -i \
    && ./configure --prefix=${PREFIX} --enable-lib-only \
    && make -j"$(nproc)" \
    && make install

RUN git clone --depth 1 --branch ${NGTCP2_VERSION} https://github.com/ngtcp2/ngtcp2.git \
    && cd ngtcp2 \
    && git submodule update --init --depth 1 \
    && autoreconf -i \
    && PKG_CONFIG_PATH="${PREFIX}/lib/pkgconfig:${AWS_LC_PREFIX}/lib/pkgconfig" \
       ./configure --prefix=${PREFIX} --enable-lib-only --with-boringssl \
        BORINGSSL_CFLAGS="-I${AWS_LC_PREFIX}/include" \
        BORINGSSL_LIBS="-L${AWS_LC_PREFIX}/lib -lssl -lcrypto" \
    && make -j"$(nproc)" \
    && make install

RUN git clone --depth 1 --branch ${NGHTTP2_VERSION} https://github.com/nghttp2/nghttp2.git \
    && cd nghttp2 \
    && git submodule update --init --depth 1 \
    && autoreconf -i \
    && PKG_CONFIG_PATH="${AWS_LC_PREFIX}/lib/pkgconfig:${PREFIX}/lib/pkgconfig" \
       LDFLAGS="-Wl,-rpath,${AWS_LC_PREFIX}/lib -Wl,-rpath,${PREFIX}/lib" \
       ./configure \
        --prefix=${PREFIX} \
        --enable-app \
        --enable-http3 \
        --disable-python-bindings \
        --without-jemalloc \
        --without-libxml2 \
        --without-mruby \
        --without-systemd \
    && make -j"$(nproc)" \
    && make install \
    && ${PREFIX}/bin/h2load --help | grep -q -- "--h3" \
    && ${PREFIX}/bin/h2load --help | grep -q -- "--output-file" \
    && ${PREFIX}/bin/h2load --help | grep -q -- "--qlog-file-base" \
    && ${PREFIX}/bin/h2load --help | grep -q -- "--connect-to" \
    && ${PREFIX}/bin/h2load --help | grep -q -- "--sni"

FROM ubuntu:${UBUNTU_VERSION}

ENV DEBIAN_FRONTEND=noninteractive
ENV PATH=/opt/h2load-http3/bin:${PATH}
ENV LD_LIBRARY_PATH=/opt/h2load-http3/lib:/opt/aws-lc/lib

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        libc-ares2 \
        libev4 \
        libstdc++6 \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /opt/aws-lc /opt/aws-lc
COPY --from=build /opt/h2load-http3 /opt/h2load-http3

LABEL org.opencontainers.image.title="ProtocolLab h2load HTTP/3"
LABEL org.opencontainers.image.description="Repo-owned h2load image built with nghttp2 HTTP/3 support for ProtocolLab external-reference benchmarks."
