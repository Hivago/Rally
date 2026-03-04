#!/bin/bash
# ============================================
# RallyAPI - Setup & Key Generation Script
# ============================================
# Run this ONCE before first docker compose up
# ============================================

set -e

echo "=========================================="
echo " RallyAPI - Initial Setup"
echo "=========================================="

# --- Step 1: Generate RSA Keys ---
echo ""
echo "[1/3] Generating RSA-2048 key pair..."

KEYS_DIR="src/RallyAPI.Host/Keys"
mkdir -p "$KEYS_DIR"

if [ -f "$KEYS_DIR/private.pem" ]; then
    echo "  Keys already exist. Skipping."
else
    openssl genpkey -algorithm RSA -out "$KEYS_DIR/private.pem" -pkeyopt rsa_keygen_bits:2048
    openssl rsa -pubout -in "$KEYS_DIR/private.pem" -out "$KEYS_DIR/public.pem"
    echo "  private.pem and public.pem generated in $KEYS_DIR/"
fi

# --- Step 2: Ensure .gitignore has keys excluded ---
echo ""
echo "[2/3] Checking .gitignore..."

if grep -q "Keys/" .gitignore 2>/dev/null; then
    echo "  .gitignore already excludes Keys/"
else
    echo "" >> .gitignore
    echo "# RSA Keys - NEVER commit these" >> .gitignore
    echo "Keys/" >> .gitignore
    echo "*.pem" >> .gitignore
    echo "  Added Keys/ and *.pem to .gitignore"
fi

# --- Step 3: Verify Docker is running ---
echo ""
echo "[3/3] Checking Docker..."

if command -v docker &> /dev/null; then
    if docker info &> /dev/null; then
        echo "  Docker is running."
    else
        echo "  Docker is installed but not running. Please start Docker Desktop."
        exit 1
    fi
else
    echo "  Docker is not installed. Please install Docker Desktop first."
    echo "  https://docs.docker.com/get-docker/"
    exit 1
fi

echo ""
echo "=========================================="
echo " Setup complete! Next steps:"
echo "=========================================="
echo ""
echo "  1. Start everything:"
echo "     docker compose up -d"
echo ""
echo "  2. Watch API logs:"
echo "     docker compose logs -f api"
echo ""
echo "  3. Wait for 'Now listening on http://+:8080'"
echo "     Then test: curl http://localhost:5000/health"
echo ""
echo "  4. EF Migrations (run inside the container):"
echo "     docker compose exec api dotnet ef database update \\"
echo "       --project /source/src/Modules/Users/RallyAPI.Users.Infrastructure \\"
echo "       --startup-project /source/src/RallyAPI.Host \\"
echo "       --context UsersDbContext"
echo ""
echo "=========================================="
