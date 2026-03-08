#!/bin/bash

# Ensure root dependencies are installed (for concurrently)
if [ ! -d "node_modules" ]; then
  echo "📦 Installing root dependencies..."
  npm install
fi

# Ensure frontend dependencies are installed
if [ ! -d "RaveRadar.Client/node_modules" ]; then
  echo "📦 Installing frontend dependencies..."
  npm install --prefix RaveRadar.Client
fi

# Start both backend and frontend
echo "🚀 Starting RaveRadar (API + Client)..."
npm start
