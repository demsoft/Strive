FROM cypress/included:15.21.1

# npm cannot install into the filesystem root (the image's default working directory)
WORKDIR /e2e
RUN npm init -y > /dev/null && npm install --no-save cypress-wait-until@3.0.2
