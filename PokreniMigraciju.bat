@echo off
title PLATA - Pokretanje Migracije (DBF -> SQLite)
echo ==========================================================
echo      PLATA - Uvoz Clipper DOS DBF podataka u SQLite
echo ==========================================================
echo.
dotnet run --project ERPiZaradeMigration\ERPiZaradeMigration.csproj c:\PLATA\PLATA\KOR28 c:\ERPiZaradeApp\plata.db
echo.
echo Migracija je zavrsena. Pritisnite bilo koji taster za izlaz...
pause > nul
