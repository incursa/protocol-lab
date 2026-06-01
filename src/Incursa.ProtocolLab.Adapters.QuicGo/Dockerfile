FROM golang:1.26-alpine AS build

WORKDIR /src

COPY go.mod go.sum ./

RUN go mod download

COPY . .

RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -trimpath -ldflags="-s -w" -o /out/quic-go-http3 ./cmd/quic-go-http3

FROM alpine:3.22

RUN apk add --no-cache ca-certificates

WORKDIR /app

COPY --from=build /out/quic-go-http3 /usr/local/bin/quic-go-http3

EXPOSE 8443/tcp
EXPOSE 8443/udp

ENV PROTOCOL_LAB_IMPLEMENTATION=quic-go-http3

ENTRYPOINT ["/usr/local/bin/quic-go-http3"]
