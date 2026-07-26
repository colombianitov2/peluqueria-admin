# Peluquería Admin 0.2.0-alpha.1

Versión preliminar pública para Windows x64. No tiene firma digital; Windows SmartScreen puede
mostrar una advertencia. Los datos se conservan en `%LocalAppData%\PeluqueriaAdmin`, fuera de la
carpeta que Velopack instala o reemplaza.

## Cambios principales

- Administración de trabajadores, sillas, cuotas semanales, pagos adelantados e historiales.
- Colaboradores, aportes de capital, porcentajes, cierres y pago completo de distribuciones.
- Ventas conectadas con existencias e inventario.
- Inventario actual, movimientos y lista mensual de compra.
- Otros ingresos, gastos, imprevistos y obligaciones recurrentes.
- Préstamos, calendarios de cuotas y pagos vinculados.
- Mantenimientos recurrentes, costos estimados y costos reales.
- Resumen mensual, snapshots de cierre, punto de equilibrio y Balance anual.
- Notas persistentes con autoguardado y recuperación de borradores.
- Exportación completa a un único archivo Excel.
- Copias automáticas y manuales, restauración segura e integridad SQLite.
- Manual integrado sin conexión con índice navegable.
- Consulta de actualizaciones mediante GitHub Releases públicos y Velopack.

## Seguridad y compatibilidad

- La aplicación no publica bases SQLite, copias, exportaciones, registros ni información real.
- La versión migra de forma aditiva las bases compatibles de `0.1.0-alpha.1`.
- La moneda operativa es USD; las migraciones no convierten numéricamente valores históricos.
- El instalador no está firmado digitalmente.
- El logotipo K&amp;V y la prueba de actualización automática entre dos Releases quedan para una
  versión posterior. No forman parte de `0.2.0-alpha.1`.
