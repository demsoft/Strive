# The Playwright image ships Chromium with all system libraries, its version matches the playwright-core dependency.
FROM mcr.microsoft.com/playwright:v1.63.0-noble

# Xvfb: a virtual screen per recording, PulseAudio: a virtual sound card per recording, ffmpeg: captures both
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg xvfb pulseaudio pulseaudio-utils \
    && rm -rf /var/lib/apt/lists/* \
    && npm install -g yarn@1.22.22 --force

WORKDIR /app

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY tsconfig.json ./
COPY src ./src
RUN yarn build && yarn install --frozen-lockfile --production --ignore-scripts && yarn cache clean

COPY docker-entrypoint.sh ./
# Windows checkouts of git can turn the line endings of the script into CRLF; Linux then cannot start it
# ("exec ./docker-entrypoint.sh: no such file or directory"), so the carriage returns are removed here
RUN sed -i 's/\r$//' docker-entrypoint.sh \
    && chmod +x docker-entrypoint.sh \
    && mkdir -p /data/recordings \
    && chown -R pwuser:pwuser /data /app

# Chromium runs with --no-sandbox as this container only ever opens the Strive web app
USER pwuser
ENV NODE_ENV=production \
    WORK_DIR=/data/recordings \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

EXPOSE 3000
VOLUME /data/recordings

ENTRYPOINT ["./docker-entrypoint.sh"]
