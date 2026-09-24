FROM python:3.12-slim
WORKDIR /app
COPY SocketIoServerPython/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt pytest
COPY SocketIoServerPython/ .
ENV PYTHONUNBUFFERED=1
CMD ["python", "canonical_client.py", "--help"]
