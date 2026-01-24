#!/bin/bash

# Kolory dla lepszej czytelności
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE} Inicjalizacja Guessnica Backend${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Sprawdź czy Docker działa
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Docker nie jest uruchomiony!${NC}"
    echo -e "${YELLOW}Uruchom Docker Desktop i spróbuj ponownie.${NC}"
    exit 1
fi

# Sprawdź czy plik .env istnieje
if [ ! -f .env ]; then
    echo -e "${YELLOW}Plik .env nie istnieje. Tworzę z domyślnymi wartościami...${NC}"
    cat > .env << 'EOF'
POSTGRES_USER=guessnica
POSTGRES_PASSWORD=guessnica
POSTGRES_DB=guessnica
ASPNETCORE_ENVIRONMENT=Development
JWT_KEY=SuperSekretnyKluczJWT1234567890!@#LongSecretKey
FACEBOOK_APP_SECRET=your_facebook_app_secret_here
EMAIL_PASSWORD=your_gmail_app_password_here
EOF
    echo -e "${GREEN}Plik .env został utworzony${NC}"
    echo -e "${YELLOW}Pamiętaj aby uzupełnić FACEBOOK_APP_SECRET i EMAIL_PASSWORD!${NC}"
    echo ""
fi

# Sprawdź czy kontenery już istnieją
if docker ps -a | grep -q guessnica-backend; then
    echo -e "${YELLOW}Kontenery już istnieją. Usuwam stare kontenery...${NC}"
    docker-compose down -v
fi

echo -e "${BLUE}Budowanie obrazów Docker...${NC}"
docker compose build --no-cache

if [ $? -ne 0 ]; then
    echo -e "${RED}Błąd podczas budowania obrazów!${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}Uruchamianie kontenerów...${NC}"
docker compose up -d

if [ $? -ne 0 ]; then
    echo -e "${RED}Błąd podczas uruchamiania kontenerów!${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}Oczekiwanie na uruchomienie bazy danych...${NC}"
sleep 10

echo ""
echo -e "${BLUE}Wykonywanie migracji bazy danych...${NC}"
docker exec -it guessnica-backend-dev dotnet ef database update --project Guessnica-backend --startup-project Guessnica-backend

if [ $? -ne 0 ]; then
    echo -e "${RED}Błąd podczas wykonywania migracji!${NC}"
    echo -e "${YELLOW}Sprawdź czy baza danych jest uruchomiona:${NC}"
    echo -e "${YELLOW}docker compose logs db${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}Inicjalizacja ról w bazie danych...${NC}"
docker exec -it guessnica-backend-dev dotnet run --project Guessnica-backend --startup-project Guessnica-backend --seed 2>/dev/null || echo -e "${YELLOW} Brak seedera - pomiń ten krok${NC}"

echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}Inicjalizacja zakończona pomyślnie!${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Aplikacja dostępna pod adresami:${NC}"
echo -e "   🔹 Backend API:  ${GREEN}http://localhost:8082${NC}"
echo -e "   🔹 Swagger UI:   ${GREEN}http://localhost:8082/swagger${NC}"
echo -e "   🔹 pgAdmin:      ${GREEN}http://localhost:8081${NC}"
echo -e "   🔹 PostgreSQL:   ${GREEN}localhost:5432${NC}"
echo ""
echo -e "${YELLOW}Przydatne komendy:${NC}"
echo -e "   docker compose logs -f app  ${BLUE}# Podgląd logów${NC}"
echo -e "   docker compose stop         ${BLUE}# Zatrzymanie${NC}"
echo -e "   docker compose down -v      ${BLUE}# Usunięcie wszystkiego${NC}"
echo ""