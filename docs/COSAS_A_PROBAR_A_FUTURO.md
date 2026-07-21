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
- [ ] Confirmar en fechas futuras la próxima cuota y el próximo pago requerido, con su fecha e importe.
- [ ] Programar un cambio de tarifa con fecha de vigencia y verificar tarifa histórica en periodos causados y tarifa nueva solo en los futuros.

## Retiro y aplicación abierta

- [ ] Retirar un trabajador y consultar después su historial completo, pagos y saldo conservado.
- [ ] Mantener la aplicación abierta durante un cambio de día y comprobar que fecha local, filtros y generación programada se actualizan al volver al módulo o pulsar **Actualizar**.

Para cada comprobación se debe usar una raíz de datos temporal, anotar fecha/hora local, zona horaria, versión, resultado esperado y resultado observado. No debe utilizarse la base real para estas pruebas.
