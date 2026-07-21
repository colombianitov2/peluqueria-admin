# Pruebas y validación

## Suite automatizada

La solución contiene cuatro proyectos de pruebas:

- Domain.Tests: reglas puras de cuotas, inventario, obligaciones, mantenimiento, reportes y cierres.
- Application.Tests: transacciones y casos de uso críticos.
- Infrastructure.Tests: SQLite, migraciones, borradores, política de durabilidad, eliminación lógica, copias, restauración, CSV y Excel.
- App.Tests: comandos y presentación segura de la interfaz WPF sin abrir una ventana real.

En la validación local consolidada de la Fase 4.4 pasan 129 pruebas: 53 de dominio, 34 de aplicación, 15 de infraestructura y 27 de interfaz. La misma suite se ejecutó en Debug y Release, para un total de 258 ejecuciones satisfactorias.

## Cobertura funcional comprobada

- primera cuota, periodos de siete días, retiro, cambio futuro de tarifa e idempotencia;
- pagos parciales y anticipados de 6, 12, 24 y 1000; deuda no negativa, crédito, cobertura de 83 semanas y faltante de 8 en la semana 84;
- deuda previa de 24 con pago de 1000 y crédito resultante de 976; múltiples pagos, tarifa futura, cuentas independientes y crédito tras retiro;
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
- libro Excel real, nombre único, hojas obligatorias, apertura, filtros, filas inmovilizadas y tipos de fecha/dinero/porcentaje;
- datos históricos, futuros, eliminados y borradores separados, neutralización de fórmulas y eliminación del temporal ante fallas;
- persistencia de borradores tras reinicio y finalización atómica junto con la operación registrada;
- `foreign_keys=ON`, WAL, `synchronous=FULL` y `busy_timeout=5000` en cada conexión SQLite.
- disposición de Administración y Ajustes en los espacios lógicos equivalentes a 1366×768 con escalas de 100 %, 125 % y 150 %.
- regla semanal definitiva sin cobro de entrada, periodos completos de siete días, vencimiento el primer sábado posterior y deuda calculada a la fecha de corte;
- sillas individuales, asignación exclusiva, disponibilidad, retiro y liberación de asignaciones vencidas;
- alta de trabajador con y sin silla, selectores independientes, cambio/retiro atómico de silla y ausencia de eventos al elegir la silla actual;
- precio de venta predeterminado, control de existencia, recompra y seis categorías visibles de inventario;
- precio sugerido por silla con gastos extraoficiales separados del balance oficial;
- filtros de actividad Hoy, semana, mes, tres meses, seis meses, año y periodo personalizado, incluido el cambio silencioso de día;
- auditoría transaccional, historial financiero del colaborador y migración conservadora desde una base `0.1.0-alpha.1`;
- ausencia del módulo Flujo de caja en navegación, interfaz y Excel, y contratos visuales de acciones contextuales;
- exportación Excel ampliada con sillas, asignaciones, gastos extraoficiales, precio sugerido, historial financiero y actividad.

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

- migración y arranque de la aplicación Release con una copia aislada de los datos de revisión de `0.1.0-alpha.1`;
- revisión visual de Inicio, Inventario, Ajustes y Resumen mensual, incluidos menús contextuales, controles de Excel y los tres gráficos;
- comprobación de que la aplicación usa exclusivamente la raíz temporal indicada mediante `PELUQUERIA_ADMIN_DATA_ROOT` y permanece abierta para revisión;
- arranque oculto del ejecutable Debug con `PELUQUERIA_ADMIN_DATA_ROOT` dirigido a una carpeta controlada dentro del worktree;
- arranque oculto del ejecutable publicado Release x64 con una raíz de datos nueva; el proceso permaneció activo, creó SQLite fuera del ejecutable y se cerró de forma controlada;
- creación de SQLite fuera del directorio del ejecutable;
- publicación autocontenida Windows x64;
- empaquetado limpio local Velopack `0.1.0-alpha.1` con bootstrap verificado, instalador, portable, paquete completo y feeds;
- auditoría NuGet directa/transitiva sin vulnerabilidades conocidas y modelo EF Core sin migraciones pendientes;
- escaneo del árbol sin secretos candidatos ni binarios rastreados por Git.
- revisión del perfil de Uso del local en la segunda pantalla, con cabecera fija, pestaña de pago, deuda y crédito visibles e historial largo con desplazamiento propio;
- integridad `ok` y última migración `20260719212257_Phase43MaintenanceRecurrence` sobre una copia temporal derivada de la base de revisión de alpha.1;
- persistencia visible de un pago de USD 12, deuda restante de USD 12, próxima cuota del 22 de julio de 2026 y silla final sin asignar en datos de demostración aislados.

## No comprobado aún

- instalación interactiva del `Setup.exe` en un equipo limpio;
- Windows 10 x64 real;
- actualización entre dos GitHub Releases y conservación de datos durante ese salto;
- firma de código y comportamiento reputacional de SmartScreen;
- comparación de reportes con datos reales aprobados por el usuario.
- integración y validación del logotipo exacto K&V Barber & Beauty, porque el archivo gráfico solicitado no estuvo disponible entre los adjuntos accesibles.

Ninguna prueba automatizada utiliza la base real ni incorpora datos permanentes.
