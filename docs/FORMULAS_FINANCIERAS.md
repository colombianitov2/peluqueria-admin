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
monto por cubrir = máximo(0, punto de equilibrio mensual completo
                            - ventas y otros ingresos esperados)
precio mensual por silla = monto por cubrir ÷ sillas ocupadas vigentes
precio semanal sugerido = precio mensual × 12 ÷ 52
```

Los pagos actuales por uso del local no se restan porque crearían una fórmula circular. Sin sillas ocupadas no se divide entre cero. Los gastos recurrentes configurados ya forman parte del punto de equilibrio, por lo que no se vuelven a sumar.

## Inventario

```text
existencia = entradas iniciales + compras - ventas - consumos + ajustes de conteo
margen informativo = venta bruta - costo promedio estimado de lo vendido
costo esperado de fila mensual = costo esperado unitario × cantidad planificada
costo real de compra mensual = costo unitario real × cantidad realmente comprada
```

La compra es la única salida de caja por adquirir inventario. Venta y consumo reducen existencias, pero el costo estimado no se registra de nuevo como salida de caja. El precio de venta escrito al comprar actualiza el valor que se cobrará al cliente y nunca se usa como costo de adquisición.

Toda fila vigente de la Lista mensual que todavía no tiene movimiento de compra es un compromiso conocido para su mes. Los indicadores internos `IsActive` y `ReserveWhenOutOfStock` de bases anteriores se ignoran en cálculos. Antes de realizarla, el punto de equilibrio usa cantidad planificada por costo esperado. Al registrar la compra, el compromiso pendiente desaparece y lo sustituye exactamente una salida real calculada con la cantidad comprada; nunca se suman ambos valores.

## Obligaciones, mantenimiento, reservas y préstamos

Una obligación o mantenimiento pendiente correspondiente al mes crea un compromiso por su saldo esperado. Cada recurrencia semanal avanza exactamente siete días desde el vencimiento inicial; mensual y anual conservan su ancla de calendario. **Crédito** es una categoría de obligación separada para reportes, pero utiliza la misma regla financiera.

```text
saldo pendiente de ocurrencia abierta = máximo(valor esperado - pagos vinculados, 0)
saldo pendiente de ocurrencia confirmada = 0
valor real de ocurrencia confirmada = suma de pagos vinculados
```

Confirmar un pago cierra la ocurrencia incluso cuando el valor real difiere del esperado. El pago real reemplaza al esperado en el resultado y no se agregan ambos. Si se reservó al cerrar, el pago posterior consume esa reserva y solo `valor real - valor reservado` ajusta el periodo de pago. Una diferencia negativa libera fondos; una positiva reduce el resultado. Un mantenimiento vencido sin estimación debe recibir un costo o una exclusión justificada antes de cerrar.

Los préstamos recibidos son financiación, no ingreso operativo. La cuota vencida o correspondiente al mes es un compromiso y el calendario conserva por separado capital e interés.

Para un préstamo de principal `P`, tasa mensual decimal `r` y `n` cuotas, el método de interés sobre saldo usa:

```text
cuota = P × r × (1 + r)^n / ((1 + r)^n - 1)   si r > 0
cuota = P / n                                   si r = 0
```

Cada periodo calcula el interés sobre el saldo de capital y la parte de capital como cuota menos interés. Para una cantidad final acordada `T`, el interés total es `T - P`, el porcentaje total es `(T - P) / P × 100` y la tasa mensual equivalente de referencia es `((T / P)^(1/n) - 1) × 100`. Todo dinero se calcula en centavos enteros; la última cuota absorbe el residuo para que la suma coincida exactamente con el total esperado.

En el método de interés fijo sobre capital inicial, el interés de cada cuota es `P × r`; el capital se divide entre las cuotas y la última absorbe los centavos residuales.

## Resumen mensual

```text
ingresos reales = alquileres cobrados + ventas + otros ingresos + demás ingresos reales
financiación = aportes de colaboradores + préstamos o créditos recibidos
total ingresado = saldo trasladado del mes anterior + ingresos reales
total gastado = inventario + gastos + gastos extraoficiales + imprevistos
              + servicios + impuestos + otras obligaciones
              + pagos de préstamos + pagos de créditos + mantenimiento
              + ganancias de colaboradores efectivamente pagadas + demás salidas
punto de equilibrio = total gastado
diferencia = total ingresado - punto de equilibrio
faltó = máximo(-diferencia, 0)
sobró = máximo(diferencia, 0)
saldo trasladado al mes siguiente = diferencia + financiación
pago calculado interno para colaboradores = máximo(resultado distribuible interno, 0) × porcentaje global
asignación individual = pago calculado para colaboradores × participación interna individual
```

Las deudas de trabajadores se muestran como **Alquileres de silla pendientes** y no entran hasta cobrarse. Los aportes y préstamos recibidos aumentan el saldo disponible, pero no la ganancia ni la diferencia contra el punto de equilibrio. Cada salida se incorpora por una sola ruta compartida para evitar doble conteo. Una obligación anual se prorratea en doce meses; diciembre absorbe el residuo de centavos y su pago consume lo acumulado sin volver a descontar el total. El saldo de un mes entra exactamente una vez como saldo inicial del siguiente. Inicio, precio sugerido por silla, Resumen mensual, Balance anual, gráficos y Excel consumen la misma clasificación.

Ejemplo aprobado: `1000 - 500 - 100 - 50 - 80 = 270`; con porcentaje global 20 %, el fondo es `54`. Una deuda de trabajador de `120` se mantiene fuera hasta su cobro. Si una reserva de electricidad de `100` se paga luego por `110`, solo `10` afecta el periodo posterior.

## Cierre y distribución

El pago positivo calculado para colaboradores se divide según participaciones internas cuya suma máxima es 100 %. Los residuos de centavos se asignan de forma determinista por `Guid`. Si la suma es inferior a 100 %, la porción no asignada queda en el saldo del local.

Un resultado cero o negativo produce fondo y pagos individuales cero; el déficit pertenece al local. Un cierre confirmado conserva mes, porcentaje global, porcentaje individual por participante, fondo, reservas, exclusiones e importes históricos. El pago al colaborador cubre exactamente el valor pendiente de la asignación congelada; no admite una cifra parcial arbitraria.

Mientras el cierre permanezca confirmado, sus totales y asignaciones guardados prevalecen sobre cambios posteriores de porcentajes o registros editables al consultar el resumen mensual, el balance anual y Excel. Al reabrir, el mes vuelve a usar la fórmula dinámica.

## Balance anual

El balance presenta enero a diciembre. Un mes confirmado usa su snapshot y no vuelve a calcularse; un mes abierto usa el cálculo financiero vigente y un mes futuro permanece en cero. Cada categoría anual es la suma de esa categoría en los doce meses. El saldo trasladado del año anterior aparece una sola vez y el saldo del último mes se traslada al año siguiente.

```text
saldo siguiente = saldo anterior + ingresos reales + financiación - gastos
```

El arrastre no convierte por sí mismo un alquiler de silla pendiente en ingreso ni un préstamo pendiente en gasto nuevo. Reabrir el año invalida su arrastre y se bloquea cuando ya existe un año posterior cerrado.

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
