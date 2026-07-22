# Cosas a probar a futuro

Esta lista reúne comportamientos que dependen del paso real del tiempo o de cruzar límites de calendario. No son defectos confirmados. Existen pruebas automatizadas con reloj controlado, pero conviene repetir estos casos manualmente cuando lleguen las fechas correspondientes y antes de declarar una versión estable.

## Filtros de historial

- [ ] Confirmar **Hoy** antes y después de un cambio de día.
- [ ] Confirmar **Esta semana** al pasar de domingo a lunes y de lunes a domingo.
- [ ] Confirmar **Este mes** durante el último día de un mes y el primero del siguiente.
- [ ] Confirmar **Últimos 3 meses** y **Últimos 6 meses** al cruzar meses de distinta duración.
- [ ] Confirmar **Este año** durante el 31 de diciembre y el 1 de enero.
- [ ] Confirmar que **Todo el historial** sigue incluyendo altas, cuotas, pagos, silla y retiro sin duplicados.

## Ciclos, cuotas y saldo a favor

- [ ] Mantener un trabajador de prueba hasta cumplirse 7, 14, 21 y más días desde su ingreso y verificar una sola cuota por cada periodo completo.
- [ ] Confirmar que ningún periodo incompleto genera deuda al ingresar o al retirar al trabajador.
- [ ] Registrar saldo a favor y observar su consumo progresivo al completarse cada periodo semanal.
- [ ] Repetir manualmente la prueba automatizada `AdvanceBalance_DecreasesOncePerAnchoredWeekAndRestartDoesNotDuplicateCharges`: saldo grande, +7 días, +varias semanas, saldo parcial, faltante exacto y reinicio sin segunda deducción.
- [ ] Confirmar en fechas futuras la próxima cuota y el próximo pago requerido, con su fecha e importe.
- [ ] Programar un cambio de tarifa con fecha de vigencia y verificar tarifa histórica en periodos causados y tarifa nueva solo en los futuros.

## Retiro y aplicación abierta

- [ ] Retirar un trabajador y consultar después su historial completo, pagos y saldo conservado.
- [ ] Mantener la aplicación abierta durante un cambio de día y comprobar que fecha local, filtros y generación programada se actualizan al volver al módulo o pulsar **Actualizar**.

Para cada comprobación se debe usar una raíz de datos temporal, anotar fecha/hora local, zona horaria, versión, resultado esperado y resultado observado. No debe utilizarse la base real para estas pruebas.
# Comprobaciones posteriores a Fase 4.7

- Mantener la aplicación abierta durante el cambio de medianoche y comprobar que la campana de mantenimiento, el saldo semanal y el snapshot mensual se renuevan sin reiniciar.
- Repetir las ocho escalas del Resumen mensual en el último día de febrero, cambio de año y zonas horarias con horario de verano.
- Confirmar que un registro migrado cuya fecha operativa no coincide con `CreatedUtc` aparece en el total diario y en el aviso “sin hora”, pero nunca en una hora inventada.
- Revisar Inicio, Colaboradores, Ventas, Inventario, Ajustes, Resumen mensual y Balance anual a 100 %, 125 % y 150 % en el segundo monitor.
- Probar una carpeta de exportación de red temporalmente desconectada: debe mostrarse el error y no debe aparecer un archivo parcial ni cambiarse de carpeta silenciosamente.
- Con datos temporales, escribir una nota, esperar menos y más que el debounce, cerrar de forma normal y abrupta y confirmar la recuperación razonable.
- Confirmar visualmente obligaciones/pagos y mantenimiento pendiente/historial con nombres muy largos y suficientes filas para activar ambas barras internas.
