# Manual de usuario

Versión documentada: **0.2.0-alpha.3**.

Peluquería Admin es una herramienta interna en USD. No es un sistema contable, fiscal ni
tributario oficial y no registra clientes, proveedores, comprobantes, medios de pago ni los
servicios personales de los trabajadores. La ayuda incluida en la aplicación funciona sin
Internet y contiene el mismo contenido esencial de este documento.

Antes de guardar, revise la fecha visible. Los importes admiten dos decimales y las cantidades
de inventario hasta tres. Un guardado correcto prepara el siguiente formulario; un error conserva
lo escrito. Un borrador recuperable nunca se cuenta como operación confirmada. Las eliminaciones
solicitadas por la interfaz exigen confirmación y son lógicas: dejan trazabilidad.

## Índice

1. [Inicio](#1-inicio)
2. [Uso del local](#2-uso-del-local)
3. [Colaboradores](#3-colaboradores)
4. [Ventas](#4-ventas)
5. [Inventario](#5-inventario)
6. [Otros ingresos](#6-otros-ingresos)
7. [Gastos](#7-gastos)
8. [Imprevistos](#8-imprevistos)
9. [Obligaciones](#9-obligaciones)
10. [Préstamos](#10-préstamos)
11. [Mantenimiento](#11-mantenimiento)
12. [Resumen mensual](#12-resumen-mensual)
13. [Gráficos](#13-gráficos)
14. [Balance anual](#14-balance-anual)
15. [Ajustes](#15-ajustes)
16. [Notas](#16-notas)
17. [Manual](#17-manual)
18. [Autoguardado y seguridad](#18-autoguardado-y-seguridad)
19. [Excel](#19-excel)
20. [Actualizaciones](#20-actualizaciones)

## 1. Inicio

**Fecha actual** usa la fecha local del equipo. **Pagos pendientes** reúne servicios, impuestos,
créditos, otras obligaciones, gastos recurrentes y cuotas de préstamos vencidas o exigibles.
**Personas con pagos pendientes por uso del local** muestra trabajadores con deuda, no saldos a
favor. **Saldo disponible del local** incluye efectivo trasladado, operaciones reales y financiación,
pero mantiene los aportes y préstamos separados de la ganancia operativa. **Cantidad faltante para
alcanzar el punto de equilibrio** usa la misma fórmula del resumen
mensual. **Precio sugerido por silla** reparte el faltante entre sillas ocupadas sin duplicar gastos
ni otros ingresos.

La campanita abre mantenimientos vencidos, de hoy y futuros, con costo estimado y acceso directo a
Mantenimiento. En **Movimientos del día**, elija fecha y pulse **Consultar**; ese es el filtro de
esta lista. Muestra hora, módulo, operación, entidad, detalle, valor y estado, del más reciente al
más antiguo. Inicio se refresca al entrar, después de operaciones relacionadas y al cambiar el día.

## 2. Uso del local

Use **Añadir silla** para crear una silla y **Añadir trabajador** para guardar nombre, fecha de
ingreso, descripción y una silla inicial opcional. Si no hay silla libre, puede asignarla después
desde el perfil.

La tarifa configurada es un importe diario exacto. Se genera un cargo por cada día de lunes a sábado
en el que el trabajador esté vigente y tenga una silla asignada. El domingo no genera cargo ni consume
saldo a favor. No se divide ni prorratea la tarifa:

`cargo semanal acumulado = suma de los cargos diarios de lunes a sábado`

Con tarifa diaria USD 50, comenzar lunes genera USD 300; miércoles, USD 200; sábado, USD 50; y
domingo, USD 0 hasta el lunes siguiente. Los cargos acumulados vencen el sábado. Cada cambio crea
una nueva vigencia con fecha y hora: los cargos ya generados conservan su importe y solo los días
nuevos usan la tarifa nueva.

El perfil muestra silla actual, tarifa diaria vigente, deuda, saldo a favor, próximo pago requerido,
cobertura estimada y próximo cobro. Acepta cualquier pago positivo; cubre primero los cargos más
antiguos y el excedente se consume en días cobrables futuros con la tarifa histórica de cada día.
Retirar la silla o **Eliminar trabajador** detiene cargos posteriores y conserva cuenta, pagos e
historial. No se generan devoluciones automáticas.

## 3. Colaboradores

Un trabajador usa una silla y paga al local; un colaborador aporta capital y participa en un fondo
de ganancia. Los aportes son financiación, no ventas ni otros ingresos. En el perfil puede crear,
editar o eliminar lógicamente aportes. Cada acción genera un movimiento independiente: **Aporte
agregado**, **Aporte editado** o **Aporte eliminado**. Editar conserva el identificador y aplica al
saldo solo la diferencia; eliminar resta el último valor activo sin borrar la historia.

El porcentaje global crea el fondo:

`fondo = máximo(resultado repartible, 0) × porcentaje global`

El porcentaje individual aplica sobre ese fondo. La suma individual activa no puede superar 100 %;
la parte no asignada queda en el local. Antes de cerrar, el pago del mes es una proyección; el cierre
congela porcentajes e importes. **Pagar ganancia completa** paga toda la asignación pendiente, no una
fracción arbitraria. Si no hay ganancia, el fondo es cero y nadie queda debiendo dinero.

## 4. Ventas

Busque y seleccione un producto vendible. La pantalla muestra existencia; escriba fecha, cantidad,
precio unitario y descripción. El total se calcula antes de confirmar. **Registrar venta** impide
superar la existencia, descuenta inventario y registra el ingreso bruto. El costo promedio y margen
son informativos y no crean otra salida de caja.

**Ventas registradas** es de solo lectura: esta versión no edita ni elimina ventas desde esa pantalla.
No registre aquí servicios personales de trabajadores.

## 5. Inventario

Las tres pestañas son:

- **Inventario actual**: busca con la lupa una fila pendiente de la Lista mensual, registra fecha,
  cantidad comprada, precio de venta si aplica y descripción; después muestra existencias.
- **Movimientos**: historial de solo lectura de entradas y salidas.
- **Lista mensual de compra**: registra producto, categoría, cantidad esperada, precio unitario o por
  paquete y descripción. Sus acciones son Agregar, Editar selección, Guardar cambios y Eliminar.

`total esperado = cantidad esperada × precio esperado`

Al comprar, `costo real del movimiento = cantidad comprada × precio de la fila`. Ese precio puede
corregirse editando la selección vinculada. `existencia = entradas acumuladas − ventas y otras salidas`.
La compra real reemplaza el compromiso previsto para que gasto y punto de equilibrio la incluyan una
sola vez. Las categorías son productos para venta, cortesías, aseo, insumos y otros productos del local.

## 6. Otros ingresos

Registre fecha, concepto, valor y descripción del dinero propio que no proviene de Ventas ni Uso del
local. Guardar lo suma como ingreso operativo. Editar corrige el mismo registro; eliminar exige
confirmación y deja trazabilidad. Aportes y préstamos recibidos no se registran aquí.

## 7. Gastos

Registre fecha, concepto, categoría, valor y descripción del egreso ordinario. Reduce el resultado,
aumenta el punto de equilibrio y forma parte del total gastado. Editar sustituye los datos de la
operación y eliminar es lógico. No duplique compras, obligaciones, mantenimientos o préstamos ya
registrados en sus módulos.

## 8. Imprevistos

Use Imprevistos para daños, reparaciones o salidas extraordinarias no planificadas; use Gastos para
egresos ordinarios. Fecha, concepto, valor, descripción, edición y eliminación funcionan del mismo
modo. El importe afecta resultado y punto de equilibrio una sola vez.

## 9. Obligaciones

**Agregar obligación** solicita nombre, tipo —Servicio, Impuesto, Crédito u Otra obligación—,
recurrencia —Sin recurrencia, Semanal, Mensual o Anual—, vencimiento inicial, valor esperado y
descripción. Semanal avanza siete días; Mensual conserva el día ancla y usa el último día de un mes
corto cuando sea necesario.

**Registrar pago** guarda fecha, valor real y descripción para la ocurrencia pendiente. El pago real
liquida esa ocurrencia aunque difiera del esperado. Puede editar obligación, editar pago y eliminar
lógicamente con confirmación; el historial permanece. Lo exigible aparece en Pagos pendientes.
Crédito identifica una obligación de pago, pero no crea financiación recibida ni calendario de préstamo.

## 10. Préstamos

Un préstamo registra capital recibido como financiación y genera calendario. Métodos disponibles:
interés mensual sobre saldo, interés fijo sobre capital inicial y cantidad final acordada. La vista
previa muestra primera y última fecha, cuota, capital, interés y saldo. Se calcula en centavos y la
última cuota absorbe el residuo.

Ejemplo: USD 1.000 recibidos y USD 1.500 acordados en 25 cuotas producen USD 60 por cuota y USD 500
de costo financiero. **Registrar cuota** paga completa la siguiente cuota. Antes del primer pago puede
editarse todo el plan; después, solo nombre y descripción. Pagos de meses abiertos admiten corrección
o eliminación lógica; un mes cerrado debe reabrirse primero.

## 11. Mantenimiento

Programe equipo, tipo, fecha prevista, costo estimado opcional, frecuencia y descripción. **Marcar como
realizado** registra fecha y costo real; este sustituye la estimación sin doble conteo. Una recurrencia
completada crea como máximo la siguiente ocurrencia. La campanita muestra pendientes; un mantenimiento
pendiente solo afecta el punto de equilibrio como proyección opcional. Puede editar pendientes y
realizados; eliminar una ocurrencia futura exige confirmación.

## 12. Resumen mensual

Las tarjetas separan saldo anterior; alquileres, ventas, otros y demás ingresos; aportes y financiación;
inventario, gastos, extraoficiales, imprevistos, servicios, impuestos, obligaciones, préstamos, créditos,
mantenimiento, colaboradores pagados y demás salidas; y finalmente total disponible, total gastado,
punto de equilibrio, faltó o sobró y saldo siguiente.

`total disponible = saldo anterior + alquileres + ventas + otros ingresos operativos`

`total gastado = inventario + gastos + extraoficiales + imprevistos + servicios + impuestos + obligaciones + préstamos + créditos + mantenimiento + colaboradores pagados + demás salidas`

`faltó o sobró = total disponible − total gastado`

La financiación aumenta el saldo disponible final, pero se presenta aparte y no se convierte en
ingreso operativo ni ganancia.

Ejemplo: USD 1.205 + USD 42,86 + USD 450 = USD 1.697,86 disponibles. USD 45 + USD 75
+ USD 60 = USD 180 gastados. Saldo siguiente: USD 1.517,86.

Alquileres pendientes son cuotas causadas y no pagadas. Pagos pendientes son compromisos conocidos
aún no pagados. Para cerrar, complete importes o marque **Ignorar**, escriba el motivo obligatorio,
pulse **Guardar exclusiones** y después **Cerrar mes**. **Reabrir mes** exige confirmación.

> **Ignorar un compromiso en el cierre no lo paga, no lo elimina y no cancela la deuda.**

Un ajuste histórico a favor puede aparecer positivo para reconciliar un snapshot cerrado con categorías
vigentes; reduce salidas y no se suma como gasto nuevo.

## 13. Gráficos

El pastel de ingresos y el pastel de gastos muestran categorías, colores y porcentajes. Las líneas
comparan ingresos frente a egresos. Use Día, Semana, Mes, Fecha específica o Año específico y la fecha
de referencia cuando aparezca. Balance anual añade líneas por mes y pasteles anuales. Todos usan los
mismos movimientos que las tarjetas; si no hay datos, no inventan porciones.

## 14. Balance anual

Seleccione un año y pulse **Consultar año**. Se suman enero a diciembre: meses cerrados usan snapshots,
abiertos usan valores actuales y futuros quedan en cero. No es una segunda consulta mensual. El detalle
muestra estado, ingresos, gastos, punto de equilibrio y faltó o sobró. Las tarjetas incluyen saldo
anterior, acumulados y saldo siguiente.

Los pagos de préstamos son salidas y se incluyen exactamente una vez en mes, total anual, punto de
equilibrio, resultado y saldo trasladado. El total anual es la suma de los doce meses. Ejemplo coherente:
USD 1.692,86 operativos − USD 715 gastados = USD 977,86; con USD 350 de aportes y USD 100 de
financiación, sin otro saldo, quedan USD 1.427,86 disponibles.

**Cerrar año** requiere confirmación y doce meses cerrados. **Reabrir año** invalida el arrastre y se
bloquea si existe un año posterior cerrado.

## 15. Ajustes

Configura tarifa diaria general, porcentaje global de colaboradores, gastos extraoficiales y carpeta de
exportación. Los valores válidos se autoguardan. Un gasto extraoficial usa nombre, importe y fecha
efectiva; afecta mensualmente resultado, punto de equilibrio, colaboradores, Balance, gráficos y Excel.
Editar corrige su vigencia y eliminar la finaliza sin borrar historia.

En una instalación nueva, todos esos campos comienzan vacíos. Vacío significa **Sin configurar** y no
equivale a cero. Es necesario escribir o seleccionar expresamente cada valor, fecha, porcentaje,
cantidad, categoría o frecuencia antes de ejecutar una operación que lo necesite. La aplicación no
reemplaza un campo vacío por cero, la fecha actual ni la primera opción de una lista.

Cambiar la tarifa diaria crea una vigencia nueva inmediatamente. No modifica cargos, meses o años
históricos ya cerrados.

Datos permite crear copias, restaurar y exportar Excel. Antes de exportar es necesario elegir
expresamente una carpeta mediante **Cambiar carpeta**.

## 16. Notas

Es un bloc único, sin límite configurado y sin ajuste automático de línea. Use las barras horizontal
y vertical. Se guarda tras una pausa, al perder el foco y al cerrar. No hay borrado automático:
seleccione y elimine manualmente el texto que no quiera conservar. Vive en SQLite y forma parte de
copias y Excel.

## 17. Manual

Abra Manual debajo de Notas, use su índice para saltar a un capítulo y la barra vertical para recorrerlo.
Es estático, funciona sin Internet y leerlo no guarda datos, movimientos ni borradores.

## 18. Autoguardado y seguridad

Ajustes válidos, porcentajes, Notas y formularios recuperables se autoguardan. Un borrador recupera
campos tras un cierre inesperado, pero no afecta cálculos. Guardar operaciones, eliminar, cerrar o
reabrir periodos, restaurar y actualizar requieren acciones explícitas y las operaciones peligrosas
incluyen confirmación.

SQLite almacena los datos. Una copia `.db` es completa y restaurable: incluye actuales, históricos,
eliminados, futuros, Notas y borradores. Hay copias automáticas, manuales, previas a migración y previas
a restauración. Reinicie después de restaurar. No borre manualmente la base, sus archivos auxiliares
ni la carpeta de copias.

## 19. Excel

Cada exportación crea un solo `.xlsx`, sin necesitar Microsoft Excel. Incluye hojas de resumen, ajustes,
operaciones, inventario, obligaciones, préstamos, cierres, históricos, eliminados, Notas y borradores
separados. Las hojas de uso del local distinguen tarifas y cargos diarios de las tablas semanales
heredadas. Usa una fecha de corte única, formatos reales y neutraliza textos que podrían ejecutarse
como fórmula. Se guarda en la carpeta configurada, con fecha/hora y sin sobrescribir.

Excel es una fotografía de consulta y no se importa ni restaura. Para recuperar use una copia `.db`.

## 20. Actualizaciones

En Ajustes, Actualizaciones puede buscar una versión pública, descargarla e **Instalar y reiniciar**.
Los datos viven fuera del ejecutable y deben conservarse. No desinstale ni borre datos para actualizar.
Si falla, mantenga la versión actual, compruebe Internet, cree una copia manual y reporte el mensaje.

La aplicación administrativa funciona sin Internet. El instalador alpha no está firmado y Windows
puede mostrar SmartScreen. El icono K&amp;V identifica el ejecutable, la ventana, los accesos directos,
el instalador y el paquete portable. Una actualización se considera correcta únicamente después de
reiniciar, confirmar la versión nueva y verificar la base de datos.
