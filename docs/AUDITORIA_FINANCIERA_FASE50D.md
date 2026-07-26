# Auditoría financiera Fase 5.0D

Fecha de corte: 2026-07-26
Alcance: fórmulas de Resumen mensual y Balance anual antes de un futuro Release.

## Método

Se cargó en modo de lectura la copia temporal usada para revisión visual y se ejecutó
`AdministrationReports.MonthlyCash` para cada mes de 2026. No se modificó la base, no se usaron
datos de alpha.1 y no se cambió una fórmula para forzar el resultado esperado.

## Reproducción mensual

Julio produjo:

- saldo anterior: USD 1.205,00;
- alquileres: USD 42,86;
- otros ingresos: USD 450,00;
- total disponible: USD 1.697,86;
- extraoficiales: USD 45,00;
- servicios: USD 75,00;
- compromisos de préstamos exigibles: USD 60,00;
- total gastado: USD 180,00;
- saldo siguiente: USD 1.517,86.

La tarjeta antes llamada **Total ingresado** sí incluía saldo trasladado. Se renombró
**Total disponible del mes** en la interfaz, filas mensuales y Excel. El cálculo no cambió.

## Causa exacta del 625 anual

La base temporal contenía un cierre histórico de mayo con USD 120,00 de salidas congeladas.
Después existía un gasto extraoficial vigente de USD 45,00 con fecha efectiva en mayo. Para
respetar el snapshot, el reporte mostraba esa categoría y generaba un ajuste de USD -45,00 en
`OtherOutflowsMinorUnits`. La interfaz invertía el signo de las salidas y lo presentaba como
**Demás salidas reales +USD 45,00**.

Por eso sumar manualmente los valores absolutos producía:

`370 + 135 + 75 + 90 + 45 = 715`

pero el cálculo firmado y reconciliado era:

`370 + 135 + 75 + 90 - 45 = 625`

Los USD 90 de préstamos ya estaban incluidos exactamente una vez. Quitarlos habría producido
USD 535, no USD 625. Cambiar el total a USD 715 habría contado dos veces el efecto de mayo y
habría roto el snapshot confirmado.

## Corrección de presentación

Cuando `OtherOutflowsMinorUnits` es negativo, la tarjeta ahora se llama
**Ajuste histórico a favor (reduce salidas)**. De este modo, el valor positivo visible no se
confunde con un gasto. Si el valor es una salida real positiva, conserva **Demás salidas reales**.
No se modificaron snapshots, redondeos, vencimientos ni reglas financieras.

## Casos protegidos por pruebas

- Ejemplo mensual: `1.205 + 42,86 + 450 - 45 - 75 - 60 = 1.517,86`.
- Ejemplo anual coherente: `1.692,86 - 715 + 350 + 100 = 1.427,86`.
- Los pagos o compromisos de préstamos forman parte de total gastado y punto de equilibrio una vez.
- El acumulado anual de préstamos coincide con la suma de los meses.
- Aportes y financiación recibida permanecen separados de ingresos operativos.
- Un mes cerrado conserva su snapshot aunque cambien datos vivos posteriores.

## Conclusión

No se encontró omisión de préstamos en el total anual. La inconsistencia era de interpretación
visual del signo del ajuste histórico. Se corrigieron la terminología y la explicación, preservando
la fórmula canónica y los cierres ya confirmados.
