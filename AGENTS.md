# Instrucciones de trabajo para agentes

## Fuente de verdad y alcance

- [docs/REQUISITOS_VIGENTES.md](docs/REQUISITOS_VIGENTES.md) es la fuente canónica del producto.
- [docs/DECISIONES_PENDIENTES.md](docs/DECISIONES_PENDIENTES.md) contiene asuntos que no deben asumirse ni implementarse hasta su resolución explícita.
- Analiza primero el problema antes de modificar. Revisa `git status` y el contexto del repositorio antes de editar.
- Preserva cambios ajenos y no uses comandos destructivos.
- No escribas fuera de la raíz real del repositorio.
- En el equipo principal de Ernesto, la ubicación esperada es `D:\Proyectos de desarrollo de Software\Peluquería programa`. En otros equipos, trabaja exclusivamente dentro de la raíz detectada del clon.

## Cambios e implementación

- Usa ramas o worktrees para cambios sustanciales cuando ya exista una base estable.
- Mantén los cambios pequeños, verificables y con un propósito único.
- No implementes requisitos marcados como pendientes ni inventes campos, indicadores o módulos.
- No cambies fórmulas financieras sin pruebas que demuestren el resultado esperado y ausencia de regresiones.
- No trates la aplicación como sistema fiscal o contable oficial.
- Justifica dependencias nuevas antes de agregarlas, incluyendo necesidad, licencia e impacto.

## Datos, actualizaciones y validación

- Nunca incluyas datos reales, secretos, certificados ni claves de firma en el repositorio.
- Protege la base de datos durante actualizaciones y migraciones, con copias previas cuando corresponda.
- Compila y ejecuta pruebas en proporción al cambio.
- Informa con precisión qué se modificó, qué se verificó y qué continúa pendiente.
- Responde los informes de este proyecto en español, salvo instrucción contraria.
