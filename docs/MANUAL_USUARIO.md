# Manual de usuario

## Primer inicio

La aplicación crea sus carpetas locales y aplica migraciones automáticamente. Inicio muestra únicamente el mes actual, obligaciones pendientes, deudas por Uso del local y el faltante mensual.

Los formularios usan fechas `AAAA-MM-DD`, meses `AAAA-MM`, importes con dos decimales y cantidades de inventario con hasta tres. Los botones deshabilitados indican que falta una selección o dato válido.

## Operación habitual

### Uso del local

Registre nombre e ingreso; la silla inicial es opcional. La fecha visible es la que se guarda y, después de cada alta, el formulario vuelve a la fecha local actual. Si se recuperó un borrador, aparece un aviso para revisar su fecha antes de guardar. Si no hay sillas vacías, el trabajador puede guardarse igualmente y asignarse después desde su perfil. Al guardar, las cuotas aplicables aparecen solo por periodos completos de siete días desde el ingreso.

El perfil permite registrar cualquier pago positivo, aunque la deuda sea cero. El excedente aparece como **Saldo a favor** y se aplica automáticamente a las próximas cuotas sin cambiar el ciclo semanal. Allí también se muestran **Deuda acumulada**, próxima cuota y valor, próximo pago requerido con fecha e importe, y cobertura estimada. Cada trabajador mantiene su cuenta independiente. El historial abre en **Todo el historial** y, después de un pago, vuelve a ese filtro para mostrarlo inmediatamente incluso si su fecha queda fuera del filtro usado antes. Al retirar una silla, el trabajador permanece activo; al retirar al trabajador, dejan de proyectarse cuotas nuevas y cualquier crédito se conserva.

La cabecera del módulo muestra total de sillas, personas vigentes, sillas disponibles y cualquier sobrecupo. Esta capacidad no aparece en Inicio.

### Colaboradores y nómina

Registre colaboradores por fechas. En la misma sección **Colaboradores**, seleccione cada persona y asigne su subporcentaje; se muestran porcentaje global, asignado, faltante, fondo total y dinero pendiente. El panel de cierres, distribuciones y pagos está integrado debajo del formulario. El cierre guarda una fotografía histórica. No se puede reabrir un cierre que ya tenga pagos; sin pagos, la reapertura invalida las asignaciones anteriores y permite crear uno nuevo sin duplicarlas.

### Inventario y ventas

Primero cree el producto; no se admiten nombres activos duplicados aunque cambien las mayúsculas. Registre existencia inicial o compra antes de vender/consumir. Una venta calcula total, costo estimado y margen; no permite existencia negativa. Una corrección tampoco puede alterar el signo, el manejo de dinero ni la cronología de forma que deje existencia negativa. El conteo físico crea un ajuste sin gasto. El plan mensual sugiere solo lo faltante y no mueve dinero hasta registrar la compra.

### Otros ingresos, gastos e imprevistos

Registre fecha, concepto y monto. No repita aquí una compra creada desde Inventario ni otro movimiento generado por un módulo específico.

### Obligaciones y mantenimiento

Las obligaciones admiten recurrencia mensual/anual y pagos parciales. Sus ocurrencias aplicables aparecen al guardar y se completan al consultar Inicio, resúmenes o exportaciones. Una recurrencia del día 31 usa el último día en meses cortos y vuelve al día 31 después. El estado se calcula. En mantenimiento, indique costo estimado para la planeación y costo/fecha real al ejecutar; el sistema evita sumar ambos.

### Reportes

- Resumen mensual: escriba el mes y pulse **Consultar**.
- Balance anual: escriba el año y consulte; verá el desglose por categoría y el indicador `Positivo` o `Negativo`.
- El módulo de Manual integrado en la aplicación queda pendiente para una fase posterior.

### Editar y eliminar

Seleccione una fila y use **Editar/Cargar** para llevar sus valores al formulario. Editar no exige confirmación de borrado. Para eliminar, marque **Confirmo eliminar** y pulse **Eliminar**; la marca se limpia después de la operación. La eliminación es lógica. No se pueden eliminar padres con historial dependiente, cierres ni asignaciones calculadas; use la reapertura segura cuando corresponda.

## Ajustes

Permite configurar tarifa semanal, porcentaje global de colaboradores, carpeta de exportación y gastos extraoficiales. La única moneda es USD y no existe presupuesto opcional. Las sillas se administran individualmente en Uso del local; cambiar tarifa solo afecta cuotas futuras.

En **Datos** puede crear/restaurar copias y crear una exportación completa a Excel. Cada pulsación genera un solo `.xlsx` en la carpeta configurada —Escritorio de forma predeterminada— y nunca genera CSV. Puede abrir el archivo o su carpeta al terminar. Reinicie después de restaurar. Los Ajustes válidos se guardan automáticamente; una entrada temporalmente inválida queda como borrador recuperable.

En **Inicio**, la campana muestra mantenimientos para hoy o vencidos y el icono de documento muestra obligaciones para hoy o vencidas. En **Ventas**, escriba cantidad y precio unitario: el total USD se actualiza antes de registrar y la cantidad no puede superar la existencia. En **Resumen mensual**, el periodo cambia la escala de los gráficos; para fecha o año específicos use el selector superior y el botón **Consultar** situado a su derecha.

En **Uso del local**, seleccione **Añadir silla** o **Añadir trabajador**. Cambiar de acción prepara la fecha local actual para evitar reutilizar una fecha anterior; un borrador recuperado conserva y muestra expresamente su propia fecha. Las tablas de trabajadores y sillas permanecen visibles independientemente del filtro de actividad. Abra un perfil con doble clic para registrar pagos anticipados, administrar la silla, retirar al trabajador o consultar **Todo el historial** del más reciente al más antiguo. Solo el historial se desplaza; la cabecera del perfil permanece fija.

En **Colaboradores**, abra un perfil con doble clic para registrar aportes de capital y consultar cierres y distribuciones. Los aportes no se cuentan como ingresos operativos.

El módulo Manual se implementará únicamente cuando las funciones estén definitivamente aprobadas; no forma parte de esta fase.

En **Actualizaciones** puede buscar, descargar e instalar una versión publicada. Sin Internet, el resto de la administración local continúa funcionando.

## Límites de esta alpha

- No es contabilidad oficial ni calcula impuestos legales.
- No registra servicios personales, clientes, proveedores, medios de pago ni comprobantes.
- El instalador no está firmado y SmartScreen puede advertirlo.
- La actualización real entre Releases no se ha verificado todavía.
