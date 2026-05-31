# ProtocolLab h2load HTTP/3 Image

This folder builds ProtocolLab's repo-owned h2load image for external-reference
HTTP/3 benchmark runs.

Local tag:

```powershell
incursa/protocol-lab-h2load-http3:local
```

Build:

```powershell
.\scripts\build\Build-H2LoadHttp3Image.ps1
```

The build follows the upstream nghttp2 HTTP/3 dependency chain: aws-lc,
nghttp3, ngtcp2, and nghttp2 configured with `--enable-http3`. The Dockerfile
pins source tags through build arguments so version changes are explicit.

The build script proves that the final image exposes:

- `h2load --version`
- `--h3`
- `--output-file`
- `--qlog-file-base`
- `--connect-to`
- `--sni`

ProtocolLab still probes the image at run time. The image is not treated as
HTTP/3-capable unless containerized `h2load --help` advertises `--h3`.

Docker h2load runs against local host-process targets use
`host.docker.internal` for container-to-host networking. ProtocolLab records the
requested target URL, effective load-tool URL, host rewrite mode, SNI value, and
certificate mode in result artifacts.
