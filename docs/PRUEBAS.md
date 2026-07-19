# Pruebas y validación

## Suite automatizada

La solución contiene cuatro proyectos de pruebas:

- Domain.Tests: reglas puras de cuotas, inventario, obligaciones, mantenimiento, reportes y cierres.
- Application.Tests: transacciones y casos de uso críticos.
- Infrastructure.Tests: SQLite, migraciones, eliminación lógica, copias, restauración y CSV.
- App.Tests: comandos y presentación segura de la interfaz WPF sin abrir una ventana real.

En la validación local de la Fase 3.1 pasan 57 pruebas: 34 de dominio, 15 de aplicación, 5 de infraestructura y 3 de interfaz.

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
- confirmación exclusiva de borrado, edición sin confirmación y presentación segura de pagos históricos;
- generación inmediata e idempotente de cuotas y recurrencias, incluido el anclaje mensual en día 31;
- reapertura transaccional, bloqueo con pagos, invalidación de asignaciones y cierre nuevo sin duplicados;
- snapshots confirmados en cálculo y CSV, y regreso al cálculo dinámico tras reabrir;
- filtros estrictos de Inicio, capacidad/sobrecupo y desglose anual con indicador;
- nombres de producto únicos, invariantes de corrección y existencia cronológica no negativa;
- protección de personas, productos, obligaciones, colaboradores, cierres y asignaciones con relaciones.

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
- arranque oculto del ejecutable publicado Release x64 con una raíz de datos nueva; el proceso permaneció activo, creó SQLite fuera del ejecutable y se cerró de forma controlada;
- creación de SQLite fuera del directorio del ejecutable;
- publicación autocontenida Windows x64;
- empaquetado limpio local Velopack `0.1.0-alpha.1` con bootstrap verificado, instalador, portable, paquete completo y feeds;
- auditoría NuGet directa/transitiva sin vulnerabilidades conocidas y modelo EF Core sin migraciones pendientes;
- escaneo del árbol sin secretos candidatos ni binarios rastreados por Git.

## No comprobado aún

- instalación interactiva del `Setup.exe` en un equipo limpio;
- Windows 10 x64 real;
- actualización entre dos GitHub Releases y conservación de datos durante ese salto;
- firma de código y comportamiento reputacional de SmartScreen;
- comparación de reportes con datos reales aprobados por el usuario.

Ninguna prueba automatizada utiliza la base real ni incorpora datos permanentes.
