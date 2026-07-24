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

## Eliminación lógica y aplicación abierta

- [ ] Eliminar lógicamente un trabajador y consultar después su historial completo, pagos y saldo conservado.
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

## Comprobaciones posteriores a Fase 4.8

- Mantener reservas abiertas durante varios cambios reales de mes y verificar consumo, liberación y diferencia adicional sin doble descuento.
- Validar un préstamo real durante toda su secuencia de cuotas, incluida la última cuota menor que el valor habitual.
- Revisar cierres mensuales y anual con doce meses reales, reaperturas y cambios históricos de porcentajes.
- Repetir la revisión visual de Inicio, Uso del local, Colaboradores, Inventario, Obligaciones, Mantenimiento, Resumen mensual, Balance anual y Ajustes a 100 %, 125 % y 150 % en el segundo monitor.
- Confirmar la migración y el rendimiento con una copia futura de gran tamaño; las pruebas actuales usan bases aisladas pequeñas y controladas.

## Comprobaciones posteriores a Fase 4.9

- Dejar una copia temporal cerrada durante uno, dos y varios sábados; al abrirla, verificar que cada cuota se generó una sola vez y consumió el saldo a favor cronológicamente.
- Repetir con saldo suficiente, saldo parcial y sin saldo; anotar saldo restante, deuda, próximo cobro, próximo pago requerido y cobertura estimada.
- Cambiar la tarifa antes de un sábado futuro y comprobar que las cuotas anteriores conservan la tarifa histórica y la nueva solo rige desde su fecha efectiva.
- Consultar movimientos cercanos a las 00:00 y durante un cambio real de fecha local; verificar que ayer, hoy y mañana nunca se mezclan.
- Pagar durante varios meses el préstamo de prueba USD 100 → USD 150 y confirmar cinco cuotas de USD 30, incluido el historial tras reiniciar.
- Cerrar un año temporal con cuentas, reservas, préstamo, superávit o déficit; comprobar el arrastre separado al año siguiente sin convertirlo en ingreso o gasto nuevo.
