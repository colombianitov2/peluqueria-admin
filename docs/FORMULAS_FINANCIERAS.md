# Fórmulas financieras

Todas las operaciones monetarias usan unidades menores enteras. Los redondeos necesarios se hacen al centavo y no alteran datos históricos.

## Uso del local

```text
deuda acumulada = máximo(suma de cuotas generadas - suma de pagos válidos, 0)
saldo a favor = máximo(suma de pagos válidos - suma de cuotas generadas, 0)
```

Al ingresar la deuda es cero. La primera cuota se causa tras siete días completos y vence el primer sábado igual o posterior al final del periodo. Los periodos siguientes avanzan siete días desde la fecha de ingreso; registrar un pago nunca reinicia ese ciclo. No se cobra un periodo incompleto y cada cuota ya causada conserva su tarifa histórica. Las cuotas futuras usan la tarifa vigente al inicio de cada periodo.

Se permite cualquier pago positivo, incluso con deuda cero. El pago se aplica primero a las cuotas causadas más antiguas. El excedente queda como saldo a favor y cubre automáticamente las próximas cuotas, por trabajador y sin mezclar cuentas. La proyección avanza hasta la primera cuota que el crédito no cubre completamente e informa en un solo dato la fecha y el importe del próximo pago requerido, además de la última fecha cubierta. Retirar al trabajador detiene las cuotas futuras, pero conserva su crédito; esta fase no implementa devoluciones.

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
sugerencia mensual = máximo(0, necesidad del mes - existencia disponible)
margen informativo = venta bruta - costo promedio estimado de lo vendido
```

La compra es la única salida de caja por adquirir inventario. Venta y consumo reducen existencias, pero el costo estimado no se registra de nuevo como salida de caja.

## Obligaciones y mantenimiento

Para la meta mensual de una obligación se usa una sola cifra:

```text
si está pagada completamente: importe real acumulado
si está pendiente o parcial: importe esperado
```

Los pagos parciales reducen el saldo, pero no duplican el gasto esperado. El flujo de caja incluye únicamente pagos reales.

Para mantenimiento se usa costo real si fue ejecutado; de lo contrario, el costo estimado cuando corresponde al mes. Nunca se suman ambos.

## Resumen mensual

```text
ingresos disponibles = pagos por Uso del local + ventas brutas + otros ingresos

reserva opcional = máximo(presupuesto mensual configurado,
                           insumos opcionales reales)

meta mensual = obligaciones aplicables
             + compras y gastos aplicables
             + imprevistos
             + mantenimiento aplicable
             + reserva opcional
             + planes mensuales pendientes aplicables

faltante = máximo(0, meta mensual - ingresos disponibles)
resultado base = ingresos disponibles - meta mensual
fondo colaboradores = máximo(0, resultado base × porcentaje)
resultado retenido = resultado base - fondo colaboradores
```

El porcentaje no cambia el punto de equilibrio cero. Las compras, obligaciones y mantenimientos se incorporan por una sola ruta para evitar doble conteo.

## Cierre y distribución

El fondo positivo se divide en partes iguales entre los participantes confirmados. Primero se asignan centavos completos; los centavos residuales se entregan uno por uno siguiendo el orden estable de `Guid`. Así, la suma de pagos individuales coincide exactamente con el fondo.

Un resultado base cero o negativo produce fondo cero y no crea deuda. Un cierre confirmado conserva mes, porcentaje, fondo, participantes e importes históricos.

Mientras el cierre permanezca confirmado, sus totales guardados prevalecen sobre cambios posteriores de porcentaje, presupuesto o registros editables al consultar el resumen mensual, el balance anual y los CSV. Al reabrir, el mes vuelve a usar la fórmula dinámica.

## Balance anual

El balance suma los 12 resultados mensuales, distribuciones pagadas de cierres confirmados y obligaciones pendientes sin repetir obligaciones anuales ya incluidas en un mes. Desglosa servicios, impuestos, otras obligaciones, mercancía, insumos obligatorios y opcionales, mantenimiento, imprevistos, otros gastos y planes de reposición. El ajuste histórico reconcilia el desglose dinámico con la meta guardada de un cierre confirmado. El indicador es `Positivo` cuando el resultado retenido acumulado es mayor o igual a cero y `Negativo` cuando es inferior a cero.

Las operaciones originales de ingresos y gastos se conservan para los cálculos internos, pero Flujo de caja ya no es un módulo visible ni una hoja independiente de Excel.

## Aportes de capital

Los aportes de colaboradores se registran en `CollaboratorContributions` y se excluyen por completo de `MonthlySummaryInput`, ingresos operativos, meta, faltante, resultado base, fondo de colaboradores y resultado retenido. Son capital/inversión histórica y no generan un porcentaje adicional.
