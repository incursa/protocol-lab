FROM nginx:alpine

RUN apk add --no-cache openssl

COPY nginx.conf /etc/nginx/nginx.conf
COPY docker-entrypoint.sh /usr/local/bin/protocol-lab-nginx-entrypoint.sh

RUN chmod +x /usr/local/bin/protocol-lab-nginx-entrypoint.sh

EXPOSE 8446/tcp
EXPOSE 8446/udp

ENTRYPOINT ["/usr/local/bin/protocol-lab-nginx-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
