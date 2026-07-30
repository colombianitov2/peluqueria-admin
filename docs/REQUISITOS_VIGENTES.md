# Requisitos vigentes

## Carácter canónico y precedencia

Este documento es la fuente canónica actual de requisitos e incorpora las decisiones aprobadas hasta la Fase 5.2 del 29 de julio de 2026.

La Fase 4.2 sustituye expresamente, cuando exista contradicción, las reglas anteriores sobre terminología del personal, pantallas genéricas de Uso del local y Colaboradores, inventario heredado, aportes de capital, número abstracto de sillas, módulo visible Flujo de caja y lista exclusiva anterior de Inicio. Las Fases 4.4 a 5.0A concretan las reglas posteriores de perfiles, inventario, cierres y aportes. La Fase 5.2 reemplaza por completo la tarifa semanal y todo prorrateo de Uso del local: rige una tarifa diaria exacta de lunes a sábado, condicionada a una asignación de silla activa, con vencimiento acumulado el sábado. También establece trazabilidad financiera separada para aportes y tarifas, saldo disponible visible, logotipo K&V y actualización real desde GitHub.

Los trabajadores son quienes usan y alquilan las sillas. Los colaboradores son exclusivamente inversionistas y nunca ocupan sillas. La interfaz, los mensajes, Excel y la documentación usan esta distinción.

- Cuando exista contradicción con ideas iniciales de `Programa para peluquería.txt`, prevalece este documento.
- `Instrucciones codex.txt` conserva las normas de trabajo y seguridad del proyecto, excepto cuando contradiga expresamente la solicitud vigente.
- Los dos archivos anteriores no estaban presentes en la carpeta del proyecto durante esta preparación. Su contenido deberá revisarse antes de comenzar la implementación para confirmar que no haya contexto adicional compatible con estos requisitos.
- No deben inventarse funciones, reglas de negocio, datos ni obligaciones que no aparezcan aquí o que el usuario no apruebe posteriormente.

## 1. Propósito y límites

La aplicación es una herramienta interna de administración para una peluquería. Su finalidad es registrar y clasificar:

- cuánto dinero entra;
- cuánto dinero sale;
- qué obligaciones existen;
- qué inventario queda;
- si el local alcanza su punto de equilibrio.

La aplicación:

- no es un sistema contable oficial;
- no está dirigida a una empresa colombiana;
- no implementará normativa fiscal colombiana;
- no preparará declaraciones de impuestos;
- no presentará información ante entidades gubernamentales.

## 2. Modelo del negocio

- Las personas que trabajan en el local llevan sus propios implementos y productos de trabajo.
- El local no paga esos implementos ni productos.
- El dinero que estas personas cobran a sus clientes por sus servicios no pertenece al local y no se registra como ingreso del local.
- Cada persona paga la tarifa diaria general configurada por utilizar el local durante un día cobrable.
- La tarifa diaria constituye un valor general configurable en Ajustes, no una tarifa individual.
- Vacío significa `Sin configurar` y no genera historial ni cargos. Cero es un valor configurado
  válido y genera cargos diarios de USD 0.
- Una instalación nueva no contiene tarifa diaria. El antiguo USD 12 ambiguo de una instalación
  existente se conserva pendiente e inactivo hasta que el administrador lo confirme, lo cambie o
  lo deje vacío; un valor heredado diferente se conserva desde la actualización hacia adelante.
- El día habitual de pago es sábado.
- Cada lunes, martes, miércoles, jueves, viernes o sábado genera el importe diario exacto cuando el trabajador está vigente y tiene silla asignada. El domingo no genera cargo ni consume saldo. Los cargos se acumulan y vencen el sábado; no existe división ni prorrateo.
- Los pagos registrados reducen la deuda de cada persona.
- La página principal muestra el nombre de cada persona con deuda y el importe adeudado.

## 3. Personas que pagan por utilizar el local

El módulo se llama **Uso del local** y no representa una relación laboral.

Datos mínimos previstos:

- nombre;
- fecha de ingreso;
- descripción opcional;
- silla individual asignada actualmente.

No existe una acción visible separada para retirar al trabajador ni una fecha de retiro editable. **Eliminar trabajador** exige confirmación, libera la silla y realiza una eliminación lógica que conserva cuenta, pagos, tarifas e historial. Los estados `Retirado` heredados continúan siendo legibles.

No incluir:

- fotografía;
- espacio de almacenamiento asignado;
- tarifa diaria individual;
- campo visible activo/inactivo;
- semanas pagadas por anticipado;
- semanas perdonadas;
- semanas suspendidas;
- motivos de suspensión;
- comprobantes;
- recibos internos;
- estados vencido, exonerado o anulado;
- medio de pago;
- consultas periódicas constantes.

El registro de un pago será simple y contendrá únicamente:

- persona;
- fecha;
- monto.
- descripción opcional.

## 4. Sillas y capacidad

Cada silla es un registro individual con nombre o número, fecha de creación, descripción opcional y, si corresponde, un único trabajador vigente asignado. Los periodos históricos de asignación se conservan para determinar exactamente qué días generan cargo. Los colaboradores son inversionistas y nunca ocupan sillas. Se muestran total de sillas, trabajadores vigentes y sillas disponibles.

No incluir:

- ubicación de silla;
- espacio de almacenamiento;
- mantenimiento individual de silla dentro de esta sección.

## 5. Ingresos del local

Los ingresos propios del local pueden provenir de:

- pagos por cargos diarios de uso del local;
- venta de agua, gaseosas u otros productos;
- venta futura de productos de belleza;
- otros ingresos registrados manualmente.

Los servicios prestados directamente por las personas a sus clientes no se registran como ingreso del local.

## 6. Inventario

El inventario usa exclusivamente estas categorías: Alimento o bebida para venta, Otro producto para venta, Cortesía para clientes, Aseo, Insumo del local y Otro producto del local. No se expone el antiguo atributo técnico de unidades en interfaz, Excel ni formularios.

Los productos destinados a venta aparecen inmediatamente en Ventas, pueden buscarse por nombre sin distinguir mayúsculas y muestran existencia y precio predeterminado. Cambiar a una categoría no vendible los retira del selector; cambiar a una categoría vendible los incorpora tras guardar, sin reiniciar.

La pestaña **Lista mensual de compra** permite agregar, editar, guardar y eliminar lógicamente productos que se desean comprar. Cada fila contiene nombre, una de las seis categorías autorizadas, cantidad esperada, precio unitario o por paquete y descripción opcional. No muestra fecha ni mes y tampoco presenta controles **Activa**, **Reservar cuando el inventario llegue a cero** o **Activar o desactivar**.

La barra **Precio total esperado** se calcula automáticamente:

```text
precio total esperado = cantidad esperada × precio unitario o por paquete
```

Ese total aparece en la Lista mensual de compra, pero no se repite en Inventario actual.

**Inventario actual** permite incorporar únicamente productos pendientes creados previamente en la Lista mensual de compra. Después de seleccionar uno mediante la búsqueda, solicita fecha agregada, cantidad comprada, precio de venta cuando la categoría sea vendible y descripción para inventario. Guardar crea o vincula el producto, registra una única compra y enlaza la fila de la lista en la misma transacción. El costo real de adquisición usa el precio unitario o por paquete definido en la lista multiplicado por la cantidad realmente comprada; el precio de venta no se interpreta como costo.

La tabla de Inventario actual muestra todas las características de la fila de compra —excepto el precio total esperado— junto con cantidad comprada, existencia actual, precio de venta, descripción de inventario y fecha agregada. El botón **Editar selección** modifica de forma conjunta la fila de la lista, el producto y su compra vinculada, validando que la existencia no quede negativa.

La pestaña **Movimientos** es exclusivamente un historial de solo lectura de entradas, compras, ventas, consumos y ajustes históricos. No contiene formularios para corregir, guardar ni eliminar movimientos. La antigua pestaña separada **Agregar al inventario** deja de existir porque la incorporación se realiza directamente desde Inventario actual.

## 6.1 Uso del local y perfiles

Uso del local conserva las tres tarjetas y usa tablas independientes de trabajadores y sillas. El selector **Acción** contiene únicamente **Añadir silla** y **Añadir trabajador**. La silla inicial del trabajador es opcional; la ausencia de sillas vacías no bloquea su alta. La fecha visible se persiste exactamente, cambiar de acción o terminar un alta prepara la fecha local actual y un borrador recuperado muestra un aviso junto con su fecha antes de guardar. El formulario no expone una acción visible para limpiar, pero conserva el borrador interno.

El selector de silla del alta y el selector del perfil son colecciones independientes. El perfil ofrece únicamente sillas activas vacías más la silla actual, permite asignar, cambiar o retirar en una transacción y mantiene al trabajador aunque quede sin silla. Seleccionar la silla actual no genera eventos duplicados.

El perfil se abre por doble clic y reúne datos, cuenta individual, tarifas históricas, pago y silla. Su cabecera permanece fija; solo el historial cronológico, separado visualmente y ordenado de más reciente a más antiguo, se desplaza y virtualiza. El filtro inicial es **Todo el historial**; registrar un pago vuelve a ese filtro para mostrar exactamente un movimiento de inmediato.

Se acepta cualquier pago positivo, incluso anticipado o superior a la deuda acumulada. La deuda acumulada nunca es negativa y el excedente queda como saldo a favor. El pago cubre primero los cargos diarios impagados más antiguos; después, el crédito se consume en días cobrables futuros usando la tarifa histórica de cada fecha. El domingo no consume saldo. La cuenta informa tarifa diaria vigente, próximo cobro del sábado, próximo pago requerido con fecha e importe y cobertura estimada. La eliminación lógica o la ausencia de silla detiene cargos nuevos, conserva el crédito y no genera devolución en esta fase.

## 6.2 Aportes de colaboradores

Cada colaborador dispone de un perfil con aportes de capital, participaciones de cierres y distribuciones. Los aportes son financiación no operativa: aumentan el saldo disponible, pero no son ventas ni otros ingresos, no aumentan la ganancia neta, no generan un nuevo porcentaje y no alteran el punto de equilibrio. Se conservan mediante eliminación lógica y se incluyen en copias, Excel e historial.

El perfil conserva una sola tabla de historial cronológico. Cada evento mantiene el identificador estable del aporte original. Agregar crea **Aporte agregado** una sola vez; editar conserva el registro, añade **Aporte editado** con valor anterior, nuevo y diferencia, y aplica a caja únicamente esa diferencia; eliminar exige confirmación, resta el último valor activo y añade **Aporte eliminado** sin borrar eventos. Los botones se deshabilitan mientras guardan y los cambios se reflejan inmediatamente en perfil, Inicio y reportes.

El perfil no muestra un selector de participaciones pendientes ni otra lista que lo sustituya. Esta simplificación visual no elimina cierres, asignaciones o pagos históricos y no cambia el comportamiento vigente de **Pagar ganancia completa**.

Los productos personales de quienes trabajan en el local no pertenecen al inventario.

El inventario funciona mediante movimientos y conteos físicos mensuales:

- existencia inicial;
- compra o entrada;
- venta;
- consumo interno;
- ajuste por conteo físico;
- existencia resultante.

Reglas:

- El sobrante de un mes pasa al siguiente.
- El sobrante no se registra nuevamente como compra ni como gasto.
- Una compra afecta el dinero disponible solamente en el mes en que realmente se pagó.
- El conteo mensual no crea un gasto.
- Los planes o sugerencias de reposición quedan obsoletos. Las compras reales continúan como movimientos y gastos; los registros históricos de planes pueden permanecer solo por compatibilidad de esquema.

No incluir en productos o ventas:

- código;
- stock objetivo;
- proveedor;
- vencimiento;
- stock mínimo;
- estado activo/inactivo;
- campos adicionales distintos de la descripción opcional autorizada.

No incluir en ninguna sección proveedor, medio de pago ni comprobante.

## 7. Gastos

Clasificar, como mínimo:

- servicios y obligaciones recurrentes;
- insumos obligatorios;
- insumos opcionales para clientes;
- compras de productos para la venta;
- mantenimiento;
- gastos imprevistos;
- otros gastos.

Los gastos imprevistos pueden añadirse en cualquier mes para daños, reparaciones o acontecimientos no planificados.

## 8. Servicios, obligaciones e impuestos

- Los servicios y obligaciones se registran manualmente con sus fechas y valores.
- Los tipos disponibles son **Servicio**, **Impuesto**, **Crédito** y **Otra obligación**.
- Las recurrencias disponibles son **Sin recurrencia**, **Semanal**, **Mensual** y **Anual**. La semanal conserva el vencimiento ancla y avanza exactamente siete días.
- Los impuestos son únicamente recordatorios y gastos internos.
- La aplicación no calcula obligaciones legales ni prepara declaraciones.
- La página principal muestra la fecha, el nombre y el saldo de las obligaciones pendientes —incluidos créditos— y de las compras mensuales conocidas que correspondan al periodo, junto con los demás elementos expresamente permitidos para esa página.
- Registrar un pago confirma una ocurrencia con el valor real. Una ocurrencia confirmada queda sin saldo pendiente aun cuando el valor real difiera del esperado; los reportes usan ese valor real una sola vez.

## 9. Mantenimiento

Debe existir una sección de mantenimiento para:

- aires acondicionados;
- equipos del local;
- otros bienes que requieran mantenimiento.

Datos que se pueden registrar:

- equipo o bien;
- tipo de mantenimiento;
- fecha prevista;
- costo estimado, aunque inicialmente sea desconocido;
- fecha realizada;
- costo real cuando se conozca.

No incluir:

- ubicación;
- estado manual;
- técnico;
- proveedor;
- campos adicionales distintos de la descripción opcional autorizada.

La necesidad de atención se calcula a partir de las fechas y de la existencia o ausencia de costo y fecha real, sin un campo manual de estado.

## 10. Resumen mensual, punto de equilibrio y traslado

Resumen mensual es el único módulo que contiene Cierre mensual. Muestra conceptos de negocio claros y no expone nombres técnicos del mecanismo histórico.

Ingresos del mes:

- saldo trasladado del mes anterior;
- alquileres cobrados;
- ventas;
- otros ingresos;
- demás ingresos reales.

La financiación se presenta separada: aportes de colaboradores y préstamos o créditos recibidos aumentan el saldo disponible, pero no son ganancia ni ingreso operativo.

Gastos del mes:

- inventario;
- gastos;
- gastos extraoficiales recurrentes;
- imprevistos;
- servicios;
- impuestos;
- otras obligaciones;
- pagos de préstamos;
- pagos de créditos;
- mantenimiento;
- ganancias de colaboradores efectivamente pagadas;
- demás salidas reales.

Reglas:

- Cada salida se incorpora una sola vez. Una compra prevista es sustituida por la compra real y no se suman ambas.
- El inventario sobrante no vuelve a contarse como gasto.
- Una obligación anual se prorratea entre doce meses. El mes de pago solo aplica la diferencia entre lo pagado y lo ya acumulado.
- Las deudas de trabajadores se muestran como **Alquileres de silla pendientes** y no son ingreso hasta cobrarse.
- Existe una sola tabla **Pagos pendientes**. Los mantenimientos pendientes permanecen en la campana y en Mantenimiento, separados de esa tabla.
- El faltante o sobrante de un mes se traslada exactamente una vez al siguiente.

Mostrar:

- total ingresado;
- total gastado;
- punto de equilibrio;
- faltó para el punto de equilibrio o sobró sobre el punto de equilibrio, nunca ambos;
- saldo trasladado al mes siguiente.

Balance anual contiene únicamente el selector de año, el cierre anual, el resumen anual y una fila por cada mes. Un mes cerrado usa su fotografía histórica, uno abierto usa el valor vigente y uno futuro permanece en cero.

## 11. Colaboradores y distribución de ganancias

Los colaboradores forman un grupo distinto de las personas que pagan por utilizar el local. Reciben conjuntamente un porcentaje de la ganancia mensual positiva.

- Porcentaje inicial: 20 %.
- El porcentaje es configurable en Ajustes con el nombre **Ganancia colaboradores**.
- La distribución se integra dentro de **Colaboradores**; no existe una opción lateral independiente de nómina.
- Cada colaborador guarda una **participación dentro del fondo** entre 0 % y 100 %. La suma de participaciones activas no puede superar 100 %, puede ser inferior y nunca se completa automáticamente.
- El porcentaje global crea primero el fondo. La participación individual se aplica después sobre ese fondo, no directamente sobre la ganancia neta.
- El cierre se confirma manualmente en Resumen mensual. Congela fórmula, valores, reservas, exclusiones, porcentajes y una asignación por colaborador; la reapertura exige confirmación e invalida asignaciones no pagadas.

Fórmulas:

```text
resultado repartible =
  ingresos operativos realmente cobrados
  - egresos pagados no provisionados anteriormente
  - nuevas reservas
  - ajustes de reservas anteriores
  - cuotas de préstamos
  - compromisos anteriores no cubiertos
```

Si el resultado base es menor o igual a cero:

- ganancia de colaboradores = 0;
- no se crea deuda con los colaboradores;
- las obligaciones indispensables pendientes continúan apareciendo por separado.

Si el resultado base es positivo:

```text
fondo colaboradores = resultado base × porcentaje global configurado
pago del mes = fondo colaboradores × porcentaje individual congelado
ganancia retenida por el local = máximo(resultado repartible, 0) - fondo colaboradores
```

No se inventará una fórmula circular.

El porcentaje de colaboradores no modifica el punto exacto donde el resultado es cero, porque cualquier porcentaje de cero sigue siendo cero. Sí modifica cuánto conserva el local después de obtener una ganancia.

## 12. Ajustes

Configurar, como mínimo:

- tarifa diaria general por uso del local, inicialmente USD 12;
- porcentaje de ganancia de colaboradores, inicialmente 20 %;
- carpeta de exportación, con el Escritorio como valor predeterminado.
- gastos recurrentes mensuales que intervienen una sola vez en punto de equilibrio, resultado, colaboradores, Balance anual, gráficos, Excel y precio sugerido por silla.

No se crean ajustes individuales que contradigan la tarifa diaria general.

La moneda única e invariable del programa es USD. Las bases antiguas configuradas en COP se normalizan a USD sin multiplicar, dividir ni convertir ningún valor numérico. El antiguo presupuesto mensual opcional queda obsoleto, se normaliza a cero y no interviene en cálculos.

Los importes de Ajustes se persisten en unidades menores enteras y los porcentajes en puntos básicos. No se usa punto flotante binario ni se aceptan silenciosamente más de dos decimales.

## 13. Balance anual

El balance usa únicamente una lista de años disponibles y el botón **Consultar año**. Muestra siempre enero a diciembre: snapshot final para un mes cerrado, cálculo vivo para uno abierto y cero para uno futuro. Permite cerrar el año solo cuando los doce meses están cerrados, incluidos meses en cero. El cierre anual no elimina años anteriores. Muestra:

- saldo trasladado del año anterior;
- ingresos acumulados por categoría;
- financiación separada;
- gastos acumulados por categoría;
- total ingresado, total gastado y punto de equilibrio anual;
- una sola cantidad faltante o sobrante;
- saldo trasladado al año siguiente;
- tabla mensual con ingresos finales, gastos finales, punto de equilibrio, faltó o sobró y estado;
- gráficos circulares y líneas de ingresos frente a gastos.

El indicador mensual es negativo cuando todavía falta dinero para cubrir las obligaciones mensuales. El indicador anual es negativo cuando el resultado acumulado, incluidas las obligaciones e impuestos anuales registrados, es inferior a cero.

## 14. Página principal

La página principal muestra exclusivamente:

- fecha actual o mes seleccionado;
- fecha, nombre y saldo de obligaciones pendientes, incluidos servicios, impuestos, créditos y otras obligaciones;
- gastos recurrentes del mes y cuotas de préstamos exigibles, sin mezclar mantenimientos;
- nombre de cada persona que debe pagos por uso del local;
- monto adeudado por cada persona;
- cantidad faltante para alcanzar el punto de equilibrio mensual.
- saldo disponible del local, separado de la ganancia operativa;
- tarifa diaria actual, tarifa diaria sugerida por silla ocupada y equivalente mensual, con explicación breve.
- campana de mantenimientos vencidos o para hoy, con acceso a Mantenimiento.
- movimientos persistidos del día con selector de fecha, hora local, módulo, operación, entidad, importe y estado.

No mostrar allí:

- gráficos;
- existencias o tablas de inventario;
- alertas de existencias, distintas del compromiso económico de una compra mensual;
- ventas;
- icono, insignia o panel emergente de obligaciones;
- elementos distintos del precio sugerido expresamente autorizado.
- nómina de colaboradores;
- tarjetas o indicadores adicionales.

## 15. Datos y seguridad

- La aplicación administra sus datos localmente y funciona sin Internet para esa administración.
- Se usará una base de datos local adecuada, preferentemente SQLite, salvo que un análisis técnico demuestre una alternativa mejor.
- Todo registro se puede editar.
- Las eliminaciones conservan historial mediante eliminación lógica interna, sin obligar a mostrar un campo de estado en la interfaz.
- Se agregan internamente fechas de creación, modificación y eliminación cuando sean necesarias.
- La base de datos real, sus copias de seguridad, archivos personales, contraseñas, tokens y certificados no se guardan en Git.

Copias y exportación:

- máximo una copia automática diaria cuando la base cambió y retención de las 30 automáticas más recientes;
- copia diferenciada antes de migrar un esquema existente y antes de restaurar;
- restauración manual después de validar compatibilidad y con recuperación de la base anterior ante fallo;
- una única exportación `.xlsx` con todas las hojas lógicas, Notas, historial, futuro conocido, eliminados y borradores; incluye tarifas diarias, cargos diarios, asignaciones, pagos y eventos financieros. Las tablas semanales anteriores se conservan en hojas marcadas como legado; no ofrece CSV en la interfaz.
- la carpeta del `.xlsx` es configurable, persistente y nunca cambia silenciosamente si ocurre un error.

La arquitectura debe contemplar:

- copias de seguridad automáticas;
- restauración manual;
- protección de datos durante actualizaciones;
- migraciones transaccionales de base de datos;
- copia de seguridad previa a migraciones importantes;
- posibilidad futura de exportar información.

## 16. Instalación y actualizaciones

El programa está dirigido inicialmente a Windows.

Primera instalación:

- debe existir un instalador ejecutable.

Después de instalado, la aplicación debe:

- buscar nuevas versiones publicadas mediante GitHub;
- descargar una actualización disponible;
- aplicarla al cerrar o reiniciar la aplicación;
- actualizarse sin exigir una desinstalación manual previa;
- conservar siempre la base de datos del usuario;
- ofrecer una acción manual **Buscar actualizaciones**.

Política de versiones y publicación:

- usar versiones semánticas;
- preparar versiones estables mediante GitHub Releases;
- automatizar compilación, pruebas, empaquetado y publicación exclusivamente mediante etiquetas SemVer `v*` deliberadas;
- no incrustar un token personal de GitHub en el ejecutable.

El proyecto utiliza un único repositorio público para el código y los lanzamientos:

- [https://github.com/colombianitov2/peluqueria-admin](https://github.com/colombianitov2/peluqueria-admin)
- El repositorio remoto ya existe y el proyecto fue publicado el 18 de julio de 2026.
- Los GitHub Releases públicos del mismo repositorio serán el canal previsto para las actualizaciones.

El ejecutable nunca debe incluir un token personal de GitHub ni otra credencial para consultar o descargar actualizaciones públicas.

La primera alpha es x64, sin certificado y puede activar una advertencia de SmartScreen. Windows 11 es la plataforma principal de validación; Windows 10 x64 sigue siendo un objetivo no verificado en un equipo real. No se declara verificada una actualización entre Releases hasta disponer de dos versiones publicadas.

La versión `0.2.0-alpha.2` incorpora el logotipo K&V original en ejecutable, ventana, accesos directos, instalador y portable. Su aceptación exige comprobar el salto real desde `0.2.0-alpha.1` mediante el actualizador interno de GitHub, sin ejecutar manualmente el instalador nuevo.

## 17. Correcciones de aceptación de la Fase 3.1

- Editar y eliminar son acciones separadas: la confirmación se exige solo para eliminar y se reinicia después de usarla.
- Al crear una persona de Uso del local se generan inmediatamente sus cuotas aplicables. Al crear una obligación recurrente se generan inmediatamente sus ocurrencias aplicables, sin reiniciar la aplicación y sin duplicados.
- Una recurrencia mensual conserva como ancla la fecha original; por ejemplo, una obligación del día 31 pasa por el último día de febrero y vuelve al día 31 cuando el mes lo permite.
- Un cambio de ingreso o retiro que invalide cuotas con pagos se rechaza. Sin pagos, las cuotas incompatibles pueden invalidarse de forma lógica y transaccional.
- Reabrir un cierre con pagos de distribución se rechaza. Sin pagos, la reapertura invalida sus asignaciones calculadas; un cierre nuevo crea una sola distribución activa cuya suma coincide exactamente con el fondo.
- Solo las asignaciones de cierres confirmados se pueden pagar o mostrar como pendientes.
- Un cierre confirmado es una fotografía histórica para el resumen mensual, el balance anual y Excel. Un mes reabierto vuelve al cálculo dinámico.
- Inicio muestra **Pagos pendientes** con servicios, impuestos, otras obligaciones y cuotas de préstamos vencidas o del mes actual; mantenimiento conserva su campana separada. También muestra deudas por Uso del local y el faltante mensual.
- La capacidad de sillas se muestra únicamente en Uso del local, incluyendo total, personas vigentes, disponibles y sobrecupo explícito.
- El balance anual y su hoja de Excel desglosan las categorías aprobadas y muestran un indicador explícito `Positivo` o `Negativo`.
- Las correcciones de inventario conservan las invariantes de cantidad, dinero y existencia cronológica no negativa. Los nombres activos de productos son únicos sin distinguir mayúsculas.
- No se permite eliminar padres con historial dependiente ni registros calculados como cierres o asignaciones. Los datos históricos huérfanos heredados se muestran con una descripción segura en vez de cerrar la pantalla.
- Los estados y categorías visibles y exportados se presentan en español.

## 18. Correcciones de aceptación de la Fase 4.1

- Todas las páginas operativas muestran actividad no editable con periodo Hoy por defecto, semana, mes, 3 meses, 6 meses, año y rango personalizado. El cambio de día se detecta al actualizar por navegación, periodo u operación; no existe sondeo constante.
- Las operaciones confirmadas crean su actividad en la misma transacción. Los estados actuales y selectores no dependen del filtro de actividad.
- La recuperación de formularios es silenciosa y no existe ningún botón visible `Limpiar formulario`; una operación válida limpia solo sus campos, mientras un error conserva la entrada y los borradores inválidos siguen protegidos.
- Ventas selecciona por identificador un producto de venta, usa su precio predeterminado y rechaza inventario negativo. Las compras reutilizan productos existentes y calculan su total.
- Ingresos, gastos, imprevistos, obligaciones y mantenimiento usan acciones directas; no existe un desplegable genérico Acción.
- Los colaboradores no ocupan sillas y su historial financiero se deriva únicamente de cierres, participaciones y pagos reales.
- Resumen mensual añade gráficos 2D de barras, composición y evolución con los mismos cálculos que las cifras.
- Flujo de caja permanece fuera de la navegación y las pantallas; Excel incluye una hoja de trazabilidad construida a partir de las operaciones fuente.
- La exportación Excel incluye sillas, asignaciones, actividad, descripciones, gastos recurrentes vigentes e históricos, precio sugerido e historial financiero de colaboradores.

## Decisiones reemplazadas en Fase 4.6

Quedan reemplazadas la moneda configurable, COP, el presupuesto mensual opcional, la nómina como sección independiente, el reparto igualitario automático y la exportación CSV múltiple. Ventas recalcula cantidad por precio editable en USD y nunca permite superar la existencia. Las gráficas responden realmente a hoy, semana, mes, tres meses, seis meses, año, fecha específica y año específico; un registro antiguo sin hora operativa verificable se incluye en totales pero no se asigna a una hora inventada.
- La decisión de Fase 4.6 mantenía el Manual como requisito pendiente; queda expresamente reemplazada por la implementación de Fase 4.10.

## Decisiones reemplazadas en Fase 4.7

Quedan reemplazados el recibo de obligaciones de Inicio, los porcentajes individuales directos sobre ganancia neta, los planes de reposición, el cierre mensual manual visible, todos los botones `Limpiar formulario`, el formulario combinado de programación/realización de mantenimiento y la tabla única que mezclaba obligaciones con pagos. Todas las tablas usan columnas fijas no redimensionables y barras internas. Inventario se divide en `Inventario`, `Movimientos` y `Agregar`; Obligaciones separa catálogo y pagos; Mantenimiento separa programación, realización, pendientes e historial; y `Notas` es un bloc único SQLite con autoguardado, copia y exportación Excel.

## Decisiones vigentes de Fase 4.8

- La regla de cierre automático anterior queda reemplazada por cierre mensual manual visible en `Resumen mensual`, con lista previa, exclusiones justificadas, reservas y reapertura confirmada.
- `Uso del local` no ofrece retiro redundante: conserva silla y eliminación lógica con historial.
- El colaborador recibe únicamente el pago completo congelado de un resultado repartible positivo; nunca asume pérdidas ni pagos parciales arbitrarios.
- La Lista mensual de compra es una entidad nueva vinculada por identificador; no reutiliza `MonthlyRestockPlans`.
- Los préstamos se administran dentro de Obligaciones y su desembolso se separa de los ingresos operativos.

## Decisiones históricas de Fase 4.9, reemplazadas parcialmente por Fase 5.2

- Movimientos del día filtra por fecha local usando `OccurredUtc`, limpia la colección y ordena de más reciente a más antiguo; las operaciones nuevas usan una acción exacta.
- La regla semanal de esta fase fue reemplazada por cargos diarios de lunes a sábado. Los registros semanales existentes se conservan como legado sin reinterpretación.
- Los eventos de aportes son inmutables. La eliminación lógica excluye el aporte del total vigente, pero conserva creación, edición, valores anterior/nuevo y eliminación.
- La Lista mensual de compra admite nombre y categoría libres, `ProductId` nulo y vínculo atómico posterior con inventario. Conteo físico y consumo no se ofrecen en Agregar al inventario; los históricos se conservan.
- Un préstamo nuevo usa exclusivamente amortización fija sobre saldo o cantidad final acordada. El calendario mensual, capital, interés, saldo y pago asociado se persisten en unidades menores enteras.
- Balance anual consulta solo el año, combina snapshots mensuales confirmados con meses abiertos en vivo, grafica 12 meses y congela un snapshot anual con arrastres separados.
- Resumen financiero vive únicamente en Resumen mensual. Los gastos recurrentes son configuraciones persistentes sin filtro temporal, admiten edición explícita y su eliminación conserva los meses históricos.

## Fase 5.0B — integración funcional final

- Inventario conserva tres pestañas. Los formularios superiores se muestran completos y las tablas comparan compra esperada con real, existencia, valor y última actualización.
- Préstamos admite interés sobre saldo, interés fijo sobre capital inicial y total final acordado. La vista previa enumera todo el calendario y los pagos abiertos se corrigen con recálculo.
- Mantenimiento mantiene una sola próxima ocurrencia, muestra vencidos/hoy/futuros en Inicio y permite corregir realizados sin generar otra recurrencia.
- Resumen mensual usa etiquetas firmadas y comprensibles. Los gastos recurrentes y las obligaciones anuales prorrateadas afectan una sola vez la fórmula compartida.
- Los gráficos son circulares por categoría para ingresos/egresos y líneas de ingresos frente a egresos. No se usan barras.
- Balance anual usa únicamente el año, doce meses, composiciones por categoría, líneas mensuales, cierre y reapertura segura.
- Notas no ajusta líneas automáticamente, conserva autoguardado y dispone de desplazamiento horizontal y vertical.
- Los cambios de datos notifican al módulo activo; no existe sondeo periódico de datos administrativos.
- Inicio añade movimientos generales diarios sin recuperar una notificación independiente de obligaciones.

## Decisiones vigentes de Fase 4.10

- **Agregar al inventario** registra siempre una compra procedente de una fila pendiente de la Lista mensual; no ofrece un selector de operación. La lupa busca por producto, categoría o mes.
- La compra solicita fecha, cantidad real, precio de venta si corresponde y descripción. El costo de caja es `costo esperado unitario × cantidad real`; el precio de venta nunca sustituye el costo de adquisición.
- La Lista mensual ofrece agregar, editar, guardar y eliminar. Los indicadores heredados de activación y reserva no se muestran ni alteran compromisos, Inicio, punto de equilibrio, cierres o reportes.
- Obligaciones incorpora **Crédito** y recurrencia **Semanal**. Una ocurrencia pagada usa el valor real y queda con saldo cero; una pendiente conserva el saldo esperado.
- Inicio, Resumen mensual, precio sugerido por silla, Balance anual y Excel consumen las mismas reglas compartidas para no duplicar ni omitir compras u obligaciones.
- **Manual** aparece debajo de Notas y explica detalladamente cada módulo, cálculos, cierres, seguridad, copias, Excel y actualizaciones. Es contenido estático del programa y no una operación de usuario.
- Excel conserva una fotografía completa y consistente de datos actuales, históricos, futuros, eliminados, borradores y estructuras heredadas que todavía existan en SQLite.
- El logotipo y la prueba del actualizador mediante una versión posterior en GitHub no forman parte de esta entrega. No se publica `alpha.2`.
