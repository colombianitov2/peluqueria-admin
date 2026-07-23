# Manual de usuario

## Primer inicio

La aplicación crea sus carpetas locales y aplica migraciones automáticamente. Inicio muestra obligaciones pendientes en su bloque normal, deudas por Uso del local, el faltante mensual y una única campana para mantenimientos vencidos o del día.

Los formularios usan fechas `AAAA-MM-DD`, meses `AAAA-MM`, importes con dos decimales y cantidades de inventario con hasta tres. Los botones deshabilitados indican que falta una selección o dato válido.

## Operación habitual

### Uso del local

Registre nombre e ingreso; la silla inicial es opcional. La fecha visible es la que se guarda y, después de cada alta, el formulario vuelve a la fecha local actual. Si se recuperó un borrador, aparece un aviso para revisar su fecha antes de guardar. Si no hay sillas vacías, el trabajador puede guardarse igualmente y asignarse después desde su perfil. Al guardar, las cuotas aplicables aparecen solo por periodos completos de siete días desde el ingreso.

El perfil permite registrar cualquier pago positivo, aunque la deuda sea cero. El excedente aparece como **Saldo a favor** y se aplica automáticamente a las próximas cuotas sin cambiar el ciclo semanal. Allí también se muestran **Deuda acumulada**, próxima cuota y valor, próximo pago requerido con fecha e importe, y cobertura estimada. Cada trabajador mantiene su cuenta independiente. El historial abre en **Todo el historial** y, después de un pago, vuelve a ese filtro para mostrarlo inmediatamente incluso si su fecha queda fuera del filtro usado antes. La pestaña **Silla** permite asignar, cambiar o retirar la silla. **Eliminar trabajador** exige confirmación, libera la silla, detiene cuotas futuras y conserva pagos, crédito e historial mediante eliminación lógica.

La cabecera del módulo muestra total de sillas, personas vigentes, sillas disponibles y cualquier sobrecupo. Esta capacidad no aparece en Inicio.

### Colaboradores y distribución

Registre colaboradores por fechas y abra el perfil con doble clic o **Abrir perfil seleccionado**. `Ganancia colaboradores (%)` crea el fondo global. En cada perfil, **Porcentaje de ganancia asignado al colaborador (%)** asigna de 0 % a 100 % del fondo; la suma activa no puede superar 100 %. La pantalla principal muestra únicamente porcentaje global, fondo total, porcentaje individual, **Pago del mes** y estado. Antes del cierre el pago es una proyección; después queda congelado. **Pagar ganancia completa** toma automáticamente la asignación mensual más antigua pendiente y no permite escribir un valor parcial. Aportes y pagos son operaciones distintas. El único historial cronológico integra ediciones, aportes, cierres, porcentajes congelados y pagos reales.

### Inventario y ventas

Primero cree el producto; no se admiten nombres activos duplicados aunque cambien las mayúsculas. Registre existencia inicial o compra antes de vender/consumir. Una venta calcula total, costo estimado y margen; no permite existencia negativa. Una corrección tampoco puede alterar el signo, el manejo de dinero ni la cronología de forma que deje existencia negativa. El conteo físico crea un ajuste sin gasto. La pestaña **Lista mensual de compra** vincula producto, cantidad, costo esperado, mes de aplicación y reserva al llegar a cero. Al registrar la compra real, aumenta inventario, consume la reserva vinculada y solo la diferencia afecta el nuevo periodo.

### Otros ingresos, gastos e imprevistos

Registre fecha, concepto y monto. No repita aquí una compra creada desde Inventario ni otro movimiento generado por un módulo específico.

### Obligaciones y mantenimiento

En **Obligaciones**, use **Agregar obligación** una sola vez para definir nombre, tipo, recurrencia, vencimiento inicial y valor esperado. Use **Registrar pago** para elegir esa obligación y guardar cada valor real; catálogo y pagos aparecen en columnas separadas. **Préstamos** vive dentro del mismo módulo: registra saldo, cuota, frecuencia y vencimiento, y cada cuota reduce el saldo. El dinero recibido es financiación, no ingreso operativo. Una recurrencia del día 31 usa el último día en meses cortos y vuelve al día 31 sin deriva. En **Mantenimiento**, programe en el recuadro izquierdo y complete un pendiente en el derecho; pendientes e historial filtrable por equipo aparecen separados abajo.

### Reportes

- Resumen mensual: consulte el mes, revise ingresos cobrados, cuentas, reservas, préstamos y resultado. Para cerrar, resuelva importes faltantes o marque **Ignorar en este cierre**, escriba el motivo, pulse **Guardar exclusiones** y luego **Cerrar mes**. Reabrir exige confirmación y no es posible si ya se pagó una distribución.
- Balance anual: escriba únicamente el año y consulte; verá enero a diciembre y el gráfico de ingresos. **Cerrar año** exige confirmación y doce meses cerrados, incluidos meses en cero.
- El módulo de Manual integrado en la aplicación queda pendiente para una fase posterior.

### Editar y eliminar

Seleccione una fila y use **Editar/Cargar** para llevar sus valores al formulario. Editar no exige confirmación de borrado. Para eliminar, marque **Confirmo eliminar** y pulse **Eliminar**; la marca se limpia después de la operación. La eliminación es lógica. No se pueden eliminar padres con historial dependiente, cierres ni asignaciones calculadas; use la reapertura segura cuando corresponda.

## Ajustes

Permite configurar tarifa semanal, porcentaje global de colaboradores, carpeta de exportación y gastos extraoficiales. La única moneda es USD y no existe presupuesto opcional. Las sillas se administran individualmente en Uso del local; cambiar tarifa solo afecta cuotas futuras.

En **Datos** puede crear/restaurar copias y crear una exportación completa a Excel. Cada pulsación genera un solo `.xlsx` en la carpeta configurada —Escritorio de forma predeterminada— y nunca genera CSV. Puede abrir el archivo o su carpeta al terminar. Reinicie después de restaurar. Los Ajustes válidos se guardan automáticamente; una entrada temporalmente inválida queda como borrador recuperable.

En **Inicio**, la campana muestra mantenimientos para hoy o vencidos; no existe un icono emergente de obligaciones. Al final, **Movimientos del día** permite consultar cualquier fecha y muestra operaciones persistidas en orden descendente. En **Ventas**, escriba cantidad y precio unitario: el total USD se actualiza antes de registrar y la cantidad no puede superar la existencia. En **Resumen mensual**, las cifras usan ingresos cobrados y una sola deducción por cada pago, reserva o ajuste. El gráfico circular separa obligaciones, préstamos y reservas, no dibuja nombres sobre el pastel y usa una leyenda desplazable.

En **Uso del local**, seleccione **Añadir silla** o **Añadir trabajador**. Cambiar de acción prepara la fecha local actual para evitar reutilizar una fecha anterior; un borrador recuperado conserva y muestra expresamente su propia fecha. Las tablas de trabajadores y sillas permanecen visibles independientemente del filtro de actividad. Abra un perfil con doble clic para registrar pagos anticipados, administrar la silla, eliminar lógicamente al trabajador o consultar **Todo el historial** del más reciente al más antiguo. Solo el historial se desplaza; la cabecera del perfil permanece fija.

En **Inventario**, las pestañas son **Inventario actual**, **Movimientos**, **Agregar al inventario** y **Lista mensual de compra**. Los planes de reposición heredados no se muestran ni se exportan. En **Agregar al inventario**, la lista inferior contiene productos creados recientemente, no movimientos.

En **Ajustes**, **Resumen financiero del mes** es informativo y usa el mismo cálculo de Resumen mensual, Colaboradores, Inicio, Balance anual y Excel. El punto de equilibrio no es editable.

En **Notas**, escriba libremente en el bloc único. Se guarda después de una pausa breve, al perder foco y al cerrar; no hay botones Guardar o Limpiar. El contenido vuelve exactamente al abrir y forma parte de SQLite, copias y Excel.

En todo el programa las tablas tienen columnas fijas y barras internas. No existe el botón **Limpiar formulario**: un guardado correcto prepara el formulario siguiente, mientras un error conserva lo escrito.

El módulo Manual se implementará únicamente cuando las funciones estén definitivamente aprobadas; no forma parte de esta fase.

En **Actualizaciones** puede buscar, descargar e instalar una versión publicada. Sin Internet, el resto de la administración local continúa funcionando.

## Límites de esta alpha

- No es contabilidad oficial ni calcula impuestos legales.
- No registra servicios personales, clientes, proveedores, medios de pago ni comprobantes.
- El instalador no está firmado y SmartScreen puede advertirlo.
- La actualización real entre Releases no se ha verificado todavía.
