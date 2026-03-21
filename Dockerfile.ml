FROM python:3.12-slim
WORKDIR /app
COPY recommendation-engine/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY recommendation-engine/ .
EXPOSE 8000
CMD ["python3", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
