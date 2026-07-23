# Fórmulas financieras

Todas las operaciones monetarias usan unidades menores enteras. Los redondeos necesarios se hacen al centavo y no alteran datos históricos.

## Uso del local

```text
deuda acumulada = máximo(suma de cuotas generadas - suma de pagos válidos, 0)
saldo a favor = máximo(suma de pagos válidos - suma de cuotas generadas, 0)
```

Al ingresar la deuda es cero. La primera cuota se causa tras siete días completos y vence el primer sábado igual o posterior al final del periodo. Los periodos siguientes avanzan siete días desde la fecha de ingreso; registrar un pago nunca reinicia ese ciclo. No se cobra un periodo incompleto y cada cuota ya causada conserva su tarifa histórica. Las cuotas futuras usan la tarifa vigente al inicio de cada periodo.

Se permite cualquier pago positivo, incluso con deuda cero. El pago se aplica primero a las cuotas causadas más antiguas. El excedente queda como saldo a favor y cubre automáticamente las próximas cuotas, por trabajador y sin mezclar cuentas. La proyección avanza hasta la primera cuota que el crédito no cubre completamente e informa en un solo dato la fecha y el importe del próximo pago requerido, además de la última fecha cubierta. Eliminar lógicamente al trabajador detiene las cuotas futuras, pero conserva su crédito e historial; esta fase no implementa devoluciones.

## Precio sugerido por silla

```text
monto por cubrir = máximo(0, meta mensual oficial
                            + gastos extraoficiales vigentes
                            - ventas y otros ingresos esperados)
precio mensual por silla = monto por cubrir ÷ sillas ocupadas vigentes
precio semanal sugerido = precio mensual × 12 ÷ 52
```

Los pagos actuales por uso del local no se restan porque crearían una fórmula circular. Sin sillas ocupadas no se divide entre cero. Los gastos extraoficiales no forman parte del Balance anual oficial.

## Inventario

```text
existencia = entradas iniciales + compras - ventas - consumos + ajustes de conteo
margen informativo = venta bruta - costo promedio estimado de lo vendido
```

La compra es la única salida de caja por adquirir inventario. Venta y consumo reducen existencias, pero el costo estimado no se registra de nuevo como salida de caja.

## Obligaciones, mantenimiento, reservas y préstamos

Una obligación o mantenimiento pendiente correspondiente al mes crea un compromiso por su valor esperado. Si se reserva al cerrar, el pago posterior consume esa reserva y solo `valor real - valor reservado` ajusta el periodo de pago. Una diferencia negativa libera fondos; una positiva reduce el resultado. Un mantenimiento vencido sin estimación debe recibir un costo o una exclusión justificada antes de cerrar.

Los préstamos recibidos son financiación. La cuota vencida o correspondiente al mes es un compromiso; para esta aplicación la cuota completa es salida de efectivo y no se separan capital e intereses.

## Resumen mensual

```text
ingresos operativos cobrados = pagos recibidos por Uso del local
                + ventas registradas
                + otros ingresos registrados
resultado repartible = ingresos operativos cobrados
                     - egresos pagados no provisionados anteriormente
                     - nuevas reservas del mes
                     - ajustes de reservas anteriores
                     - cuotas de préstamos del mes
                     - compromisos anteriores no cubiertos
fondo colaboradores = máximo(resultado repartible, 0) × porcentaje global
asignación individual = fondo colaboradores × participación interna individual
ganancia retenida por el local = máximo(resultado repartible, 0) - fondo colaboradores
faltante = máximo(-resultado repartible, 0)
```

Las cuentas por cobrar no entran hasta cobrarse. Los aportes y préstamos recibidos aumentan la disponibilidad, pero no el resultado repartible. El porcentaje no cambia el punto de equilibrio cero. Compras, obligaciones, mantenimientos, préstamos y reservas se incorporan por una sola ruta para evitar doble conteo.

Ejemplo aprobado: `1000 - 500 - 100 - 50 - 80 = 270`; con porcentaje global 20 %, el fondo es `54`. Una deuda de trabajador de `120` se mantiene fuera hasta su cobro. Si una reserva de electricidad de `100` se paga luego por `110`, solo `10` afecta el periodo posterior.

## Cierre y distribución

El fondo positivo se divide según participaciones internas cuya suma máxima es 100 %. Los residuos de centavos se asignan de forma determinista por `Guid`. Si la suma es inferior a 100 %, la porción no asignada queda retenida por el local.

Un resultado cero o negativo produce fondo y pagos individuales cero; el déficit pertenece al local. Un cierre confirmado conserva mes, porcentaje global, porcentaje individual por participante, fondo, reservas, exclusiones e importes históricos. El pago al colaborador cubre exactamente el valor pendiente de la asignación congelada; no admite una cifra parcial arbitraria.

Mientras el cierre permanezca confirmado, sus totales y asignaciones guardados prevalecen sobre cambios posteriores de porcentajes o registros editables al consultar el resumen mensual, el balance anual y Excel. Al reabrir, el mes vuelve a usar la fórmula dinámica.

## Balance anual

El balance presenta los 12 cierres mensuales del año, usando cero y `Sin cerrar` cuando falta un snapshot. Suma ingresos cobrados, egresos, reservas, obligaciones, préstamos, fondo y resultado congelados; conserva el desglose por categorías como control. El indicador es `Positivo` cuando el resultado acumulado es mayor o igual a cero y `Negativo` cuando es inferior a cero.

Las operaciones originales de ingresos y gastos se conservan para los cálculos internos. Flujo de caja no es un módulo visible, pero se exporta como hoja de trazabilidad en Excel.

## Distribución individual vigente desde Fase 4.7

Cada importe individual se calcula sobre el fondo global:

```text
fondo global = máximo(resultado base, 0) × porcentaje global
asignación individual = fondo global × participación interna individual
```

La suma de participaciones internas puede ser menor, pero nunca mayor, que 100 %. El faltante no se reparte automáticamente. Con USD 1.000, porcentaje global 20 % y participaciones 60 %, 20 %, 10 % y 10 %, el fondo es USD 200 y los importes son exactamente USD 120, USD 40, USD 20 y USD 20.

## Aportes de capital

Los aportes de colaboradores se registran en `CollaboratorContributions` y se excluyen por completo de `MonthlySummaryInput`, ingresos operativos, meta, faltante, resultado base, fondo de colaboradores y resultado retenido. Son capital/inversión histórica y no generan un porcentaje adicional.
