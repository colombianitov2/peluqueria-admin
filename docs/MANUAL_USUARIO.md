# Manual de usuario

## Primer inicio

La aplicación crea sus carpetas locales y aplica migraciones automáticamente. Inicio muestra **Pagos pendientes** —servicios, impuestos, créditos, otras obligaciones, gastos recurrentes y cuotas de préstamos—, deudas por Uso del local, el faltante mensual y una única campana para mantenimientos vencidos, de hoy y futuros con costo estimado. Las compras previstas permanecen en Inventario y los mantenimientos pendientes no se mezclan con Pagos pendientes.

Los formularios usan fechas `AAAA-MM-DD`, meses `AAAA-MM`, importes con dos decimales y cantidades de inventario con hasta tres. Los botones deshabilitados indican que falta una selección o dato válido.

## Operación habitual

### Uso del local

Registre nombre e ingreso; la silla inicial es opcional. La fecha visible es la que se guarda y, después de cada alta, el formulario vuelve a la fecha local actual. Si se recuperó un borrador, aparece un aviso para revisar su fecha antes de guardar. Si no hay sillas vacías, el trabajador puede guardarse igualmente y asignarse después desde su perfil. Al guardar, las cuotas aplicables aparecen solo por periodos completos de siete días desde el ingreso.

El perfil permite registrar cualquier pago positivo, aunque la deuda sea cero. El excedente aparece como **Saldo a favor** y se aplica automáticamente a las próximas cuotas sin cambiar el ciclo semanal. Allí también se muestran **Deuda acumulada**, próxima cuota y valor, próximo pago requerido con fecha e importe, y cobertura estimada. Cada trabajador mantiene su cuenta independiente. El historial abre en **Todo el historial** y, después de un pago, vuelve a ese filtro para mostrarlo inmediatamente incluso si su fecha queda fuera del filtro usado antes. La pestaña **Silla** permite asignar, cambiar o retirar la silla. **Eliminar trabajador** exige confirmación, libera la silla, detiene cuotas futuras y conserva pagos, crédito e historial mediante eliminación lógica.

La cabecera del módulo muestra total de sillas, personas vigentes, sillas disponibles y cualquier sobrecupo. Esta capacidad no aparece en Inicio.

### Colaboradores y distribución

Registre colaboradores por fechas y abra el perfil con doble clic o **Abrir perfil seleccionado**. `Ganancia colaboradores (%)` crea el fondo global. En cada perfil, **Porcentaje de ganancia asignado al colaborador (%)** asigna de 0 % a 100 % del fondo; la suma activa no puede superar 100 %. La pantalla principal muestra únicamente porcentaje global, fondo total, porcentaje individual, **Pago del mes** y estado. Antes del cierre el pago es una proyección; después queda congelado. **Pagar ganancia completa** toma automáticamente la asignación mensual más antigua pendiente y no permite escribir un valor parcial. No existe un selector adicional para ese pago.

Aportes y pagos son operaciones distintas. El único historial cronológico integra ediciones, aportes, cierres, porcentajes congelados y pagos reales. Seleccione una fila **Aporte original** o **Aporte editado** cuyo aporte continúe vigente para cargar su fecha, valor y descripción. El botón principal cambia a **Guardar edición**; al guardar se actualizan inmediatamente el total y el historial, que conserva los valores anteriores. Para eliminar, seleccione el aporte, marque **Confirmo eliminar** y pulse **Eliminar aporte**. El registro deja de sumar, pero permanece como **Aporte eliminado**. Las filas de cierres, pagos y aportes ya eliminados no habilitan estos botones.

### Inventario y ventas

No se admiten nombres activos duplicados aunque cambien las mayúsculas. Una venta calcula total, costo estimado y margen y no permite existencia negativa.

En **Lista mensual de compra**, pulse **Agregar** para registrar nombre, categoría, mes, cantidad prevista, costo esperado unitario y descripción. Para corregir una fila, selecciónela, pulse **Editar**, cambie los campos y pulse **Guardar**. Para retirarla, marque **Confirmo eliminar** y pulse **Eliminar**. No existen activación, desactivación ni reserva al agotarse: toda fila no comprada representa una compra conocida para su mes.

En **Agregar al inventario** siempre se registra una compra. Use la lupa para buscar por nombre, categoría o mes entre las filas mensuales pendientes y seleccione una. Indique la fecha y cantidad realmente comprada, el precio de venta si se trata de un producto vendible y una descripción opcional. El programa toma el costo esperado unitario de la fila mensual y calcula `costo de compra = costo esperado unitario × cantidad real`; el precio de venta es lo que se cobrará al cliente y no reemplaza ese costo. **Registrar compra** crea o vincula el producto, añade existencias y marca la fila como comprada en una sola operación. Una fila comprada deja de aparecer en la búsqueda y no se vuelve a contabilizar como compromiso.

### Otros ingresos, gastos e imprevistos

Registre fecha, concepto y monto. No repita aquí una compra creada desde Inventario ni otro movimiento generado por un módulo específico.

### Obligaciones y mantenimiento

En **Obligaciones**, use **Agregar obligación** una sola vez para definir nombre, tipo —Servicio, Impuesto, Crédito u Otra obligación—, recurrencia —Sin recurrencia, Semanal, Mensual o Anual—, vencimiento inicial y valor esperado. Semanal crea vencimientos cada siete días; mensual conserva el día ancla, por lo que una obligación del día 31 usa el último día en meses cortos y vuelve al 31 cuando exista.

Use **Registrar pago** para elegir la serie y guardar el valor real de la ocurrencia pendiente más antigua. Confirmar el pago deja esa ocurrencia con saldo cero aunque el valor real resulte menor o mayor que el esperado; Inicio, cierres, reportes y Excel usan el valor real una sola vez. Las ocurrencias siguientes conservan su propio valor esperado. Para corregir un pago, selecciónelo en **Pagos registrados**, pulse **Editar pago**, modifique fecha, obligación, valor o descripción y pulse **Guardar pago**. Para retirarlo, marque **Confirmo eliminar pago** y pulse **Eliminar pago**; el vencimiento vuelve a quedar pendiente y todos los saldos se refrescan al terminar.

**Añadir préstamo** permite elegir interés mensual sobre saldo, interés fijo sobre capital inicial o cantidad final acordada. La vista previa muestra todas las cuotas con fecha, capital, interés y saldo. Antes del primer pago puede editarse todo el plan; después solo nombre y descripción. Los pagos de meses abiertos se pueden corregir o eliminar con recálculo. El dinero recibido es financiación, no ingreso operativo.

### Reportes

- Resumen mensual: consulte el mes y revise saldo trasladado, ingresos reales, financiación separada, gastos por categoría, total ingresado, total gastado, punto de equilibrio, faltó o sobró y saldo siguiente. Las deudas de trabajadores aparecen como **Alquileres de silla pendientes** y existe una sola tabla **Pagos pendientes**. Para cerrar, resuelva importes faltantes o marque **Ignorar en este cierre**, escriba el motivo, pulse **Guardar exclusiones** y luego **Cerrar mes**. Reabrir exige confirmación y no es posible si ya se pagó una distribución.
- Balance anual: seleccione únicamente un año disponible y pulse **Consultar año**; verá una fila por cada mes, líneas de ingresos/gastos y composiciones circulares. Un mes cerrado usa su snapshot, uno abierto se actualiza en vivo y uno futuro permanece en cero. **Cerrar año** exige confirmación y doce meses cerrados; **Reabrir año** invalida el arrastre y se bloquea si existe un año posterior cerrado.
- Manual: abra **Manual**, debajo de Notas, para consultar sin conexión la explicación detallada de todos los módulos, cálculos, copias, Excel, cierres y actualizaciones. Es contenido de ayuda; leerlo no modifica datos.

### Editar y eliminar

Seleccione una fila y use **Editar/Cargar** para llevar sus valores al formulario. Editar no exige confirmación de borrado. Para eliminar, marque **Confirmo eliminar** y pulse **Eliminar**; la marca se limpia después de la operación. La eliminación es lógica. No se pueden eliminar padres con historial dependiente, cierres ni asignaciones calculadas; use la reapertura segura cuando corresponda.

## Ajustes

Permite configurar tarifa semanal, porcentaje global de colaboradores, carpeta de exportación y gastos recurrentes persistentes. Cada gasto recurrente afecta mensualmente el punto de equilibrio, resultado, colaboradores, Balance anual, gráficos, Excel y precio sugerido desde su fecha inicial. Eliminar finaliza su vigencia sin borrar los meses históricos. La única moneda es USD y no existe presupuesto opcional.

En **Datos** puede crear/restaurar copias y crear una exportación completa a Excel. Cada pulsación genera un solo `.xlsx` en la carpeta configurada —Escritorio de forma predeterminada— y nunca genera CSV. El libro incluye operaciones actuales, históricos, futuro conocido, tarifas semanales, lista mensual, compras, créditos, recurrencias, saldos, cierres, eliminados, borradores separados y datos heredados todavía presentes. Puede abrir el archivo o su carpeta al terminar. Reinicie después de restaurar. Los Ajustes válidos se guardan automáticamente; una entrada temporalmente inválida queda como borrador recuperable.

En **Inicio**, la campana muestra mantenimientos vencidos, de hoy y futuros. Al final, **Movimientos del día** consulta la fecha local elegida, reemplaza la lista anterior y muestra operaciones exactas en orden descendente. En **Ventas**, escriba cantidad y precio unitario: el total USD se actualiza antes de registrar y la cantidad no puede superar la existencia. En **Resumen mensual**, las cifras usan ingresos cobrados y una sola deducción por cada pago, compromiso o diferencia.

En **Uso del local**, seleccione **Añadir silla** o **Añadir trabajador**. Cambiar de acción prepara la fecha local actual para evitar reutilizar una fecha anterior; un borrador recuperado conserva y muestra expresamente su propia fecha. Las tablas de trabajadores y sillas permanecen visibles independientemente del filtro de actividad. Abra un perfil con doble clic para registrar pagos anticipados, administrar la silla, eliminar lógicamente al trabajador o consultar **Todo el historial** del más reciente al más antiguo. Solo el historial se desplaza; la cabecera del perfil permanece fija.

En **Inventario**, las pestañas son **Inventario actual**, **Movimientos** y **Lista mensual de compra**. La primera integra el formulario para agregar desde la lista y compara cantidades/costos esperados con los reales, existencia, valor y última actualización. Los indicadores heredados de activación y reserva no aparecen ni alteran cálculos; solo se conservan como compatibilidad técnica.

El **Resumen financiero del mes** existe una sola vez, en **Resumen mensual**, y usa el mismo cálculo compartido por Colaboradores, Inicio, Balance anual y Excel. `Total ingresado = saldo anterior + alquileres cobrados + ventas + otros ingresos reales`. `Total gastado` y `Punto de equilibrio` reúnen inventario, gastos, extraoficiales, imprevistos, servicios, impuestos, obligaciones, préstamos, créditos, mantenimiento, ganancias de colaboradores efectivamente pagadas y demás salidas. La diferencia muestra únicamente **Faltó** o **Sobró**. Aportes y préstamos recibidos aumentan el saldo disponible, pero permanecen separados de la ganancia.

En **Notas**, escriba libremente en el bloc único sin límite configurado ni ajuste automático de línea; use las barras horizontal y vertical. Se guarda después de una pausa breve, al perder foco y al cerrar; no hay botones Guardar o Limpiar. El contenido vuelve exactamente al abrir y forma parte de SQLite, copias y Excel.

En **Manual**, use el desplazamiento interno y sus títulos para localizar la sección deseada. El manual no se edita, no es una nota, no se exporta como dato del negocio y no genera movimientos.

En todo el programa las tablas tienen columnas fijas y barras internas. No existe el botón **Limpiar formulario**: un guardado correcto prepara el formulario siguiente, mientras un error conserva lo escrito.

En **Actualizaciones** puede buscar, descargar e instalar una versión publicada. Sin Internet, el resto de la administración local continúa funcionando.

## Límites de esta alpha

- No es contabilidad oficial ni calcula impuestos legales.
- No registra servicios personales, clientes, proveedores, medios de pago ni comprobantes.
- El instalador no está firmado y SmartScreen puede advertirlo.
- La actualización real entre Releases no se ha verificado todavía.
- El logotipo de la empresa y la prueba del salto de actualización se incorporarán en una versión posterior; esta fase no publica `alpha.2`.
