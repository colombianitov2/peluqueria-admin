# Pruebas y validación

## Suite automatizada

La solución contiene tres proyectos:

- Domain.Tests: reglas puras de cuotas, inventario, obligaciones, mantenimiento, reportes y cierres.
- Application.Tests: transacciones y casos de uso críticos.
- Infrastructure.Tests: SQLite, migraciones, eliminación lógica, copias, restauración y CSV.

En la validación de la Fase 3 pasan 41 pruebas: 32 de dominio, 4 de aplicación y 5 de infraestructura.

## Cobertura funcional comprobada

- primera cuota, periodos de siete días, retiro, cambio futuro de tarifa e idempotencia;
- pago parcial, deuda y rechazo de sobrepago;
- existencia inicial, compra, venta, consumo, conteo, sobrante, reposición e inventario negativo;
- obligaciones esperadas/reales, recurrencia mensual/anual y no duplicación;
- mantenimiento estimado frente a real;
- ingresos, gastos, imprevistos, punto de equilibrio positivo/negativo y caja;
- porcentaje, resultado cero, distribución exacta de centavos e inmutabilidad del cierre;
- balance anual y eliminación lógica;
- migración desde `InitialSettings` conservando la configuración;
- copia/restauración y cinco exportaciones CSV sobre datos controlados.

## Comandos finales

```powershell
dotnet tool restore
dotnet restore PeluqueriaAdmin.sln
dotnet format PeluqueriaAdmin.sln --verify-no-changes --no-restore
dotnet build PeluqueriaAdmin.sln -c Debug --no-restore
dotnet build PeluqueriaAdmin.sln -c Release --no-restore
dotnet test PeluqueriaAdmin.sln -c Release --no-build
dotnet list PeluqueriaAdmin.sln package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes --project src/PeluqueriaAdmin.Infrastructure --startup-project src/PeluqueriaAdmin.Infrastructure
```

## Validaciones manuales realizadas

- arranque oculto del ejecutable Debug con `PELUQUERIA_ADMIN_DATA_ROOT` dirigido a una carpeta controlada dentro del worktree;
- creación de SQLite fuera del directorio del ejecutable;
- publicación autocontenida Windows x64;
- empaquetado local Velopack `0.1.0-alpha.1` con bootstrap verificado.

## No comprobado aún

- instalación interactiva del `Setup.exe` en un equipo limpio;
- Windows 10 x64 real;
- actualización entre dos GitHub Releases y conservación de datos durante ese salto;
- firma de código y comportamiento reputacional de SmartScreen;
- comparación de reportes con datos reales aprobados por el usuario.

Ninguna prueba automatizada utiliza la base real ni incorpora datos permanentes.

