# Bloques de construcción — copiar tal cual

Cada bloque está escrito exactamente como debe salir en el archivo. Son los que produce el propio
editor de KnowledgeHub o los que un autor lleva años usando en él: el lector los pinta bien, el PDF
los imprime bien y el saneador los deja pasar. Inventar variantes es la forma más fácil de que algo
se pierda al guardar.

## Encabezados y aire entre secciones

```html
<h1>Título del Documento</h1>
<p>Primer párrafo: qué es esto y para quién. Sin encabezado intermedio.</p>

<h2 style="margin-top: 1.6em">Primera Sección</h2>
<h3 style="margin-top: 1.2em">Subsección</h3>
```

- Un solo `<h1>`, en la primera línea. Es el título de la página en KnowledgeHub y lo que hace que
  el PDF no ponga el suyo encima.
- Title Case en español: mayúscula en sustantivos, verbos y adjetivos, minúscula en artículos y
  preposiciones (`Las Cuatro Capas`, `Quién Puede Llamar a Quién`, `Cómo Descubre la Aplicación
  sus Extensiones`).
- El aire lo da el `margin-top`, no encabezados vacíos: un `<h2><br></h2>` de relleno acaba como una
  línea en blanco en el PDF y como un hueco raro en el índice.
- Nada por debajo de `<h3>`. Si hace falta más profundidad, el documento pide dividirse.

## Prosa

```html
<p>Presione <strong>INICIAR SESIÓN</strong> y capture su credencial. Si la contraseña no coincide,
la aplicación muestra <em>"Las contraseñas no coinciden"</em> y vuelve al campo.</p>
```

- `<strong>` para todo nombre de cosa: botones, menús, tablas, proyectos, módulos, columnas. Los
  nombres de UI van **en su idioma original**, sin traducir (`<strong>Set parameters and print</strong>`).
- `<em>` solo para citar literales de pantalla, entre comillas.
- `<code>` para GUIDs, valores de configuración, rutas cortas, nombres de columna en línea.
- Usted, siempre. Verbos de pantalla táctil: **presione**, **seleccione**, **capture**, **escriba**;
  nunca «haga clic» ni «tú». Sin emojis en la prosa.
- Líneas envueltas a unas 100 columnas dentro del `<p>`: el editor las une, y a la IA y a la
  persona que revisa el archivo les resulta legible.

## Callouts (los tres del editor)

El editor genera exactamente esto (icono, título, colores y el `<p><br></p>` final, que existe para
que el cursor pueda salir de la caja al pulsar Enter). Al guardar, KnowledgeHub normaliza los colores
a `rgba()`; es esperado.

```html
<div class="kh-callout" style="background:#eff6ff;border:1px solid #bfdbfe;border-left:5px solid #3b82f6;border-radius:8px;padding:16px;margin:8px 0;"><p style="margin:0;"><strong>💡 Nota:</strong> Texto de la nota.</p></div><p><br></p>

<div class="kh-callout" style="background:#fffbeb;border:1px solid #fde68a;border-left:5px solid #f59e0b;border-radius:8px;padding:16px;margin:8px 0;"><p style="margin:0;"><strong>⚠️ Advertencia:</strong> Texto de la advertencia.</p></div><p><br></p>

<div class="kh-callout" style="background:#fef2f2;border:1px solid #fecaca;border-left:5px solid #ef4444;border-radius:8px;padding:16px;margin:8px 0;"><p style="margin:0;"><strong>❗ Importante:</strong> Texto del punto importante.</p></div><p><br></p>
```

Cuándo cada uno: **Nota** = contexto útil que no cambia lo que hay que hacer; **Advertencia** = algo
que puede salir mal si no se tiene en cuenta; **Importante** = un paso que no es opcional o una
consecuencia irreversible. Uno por sección como mucho. Un documento lleno de cajas rojas no avisa de
nada. El callout va en **una sola línea física**; el texto de dentro sí puede llevar `<strong>`,
`<code>` y enlaces.

Otros colores válidos del editor, por si hace falta distinguir (mismo esquema fondo/borde/acento):
verde `#ecfdf5 / #a7f3d0 / #10b981` (✅), morado `#f5f3ff / #ddd6fe / #8b5cf6`, gris `#f8fafc /
#e2e8f0 / #64748b`.

## Consejo (blockquote)

Para lo que es recomendación o contexto de versión, no aviso:

```html
<blockquote><p><strong>Consejos:</strong> Los datos de este documento corresponden a la versión
<strong>3.3.0.7</strong>. La versión instalada se lee en la esquina inferior derecha del menú
principal. Conviene confirmarla antes de comparar contra lo aquí descrito.</p>
</blockquote>
```

## Detalle técnico (caja gris, clase `kh-tech`)

Para lo que un usuario final puede saltarse y un técnico necesita. Requiere que el anfitrión permita
el prefijo `kh-`; si no, usa el callout gris de arriba.

```html
<div class="kh-tech" style="background:#f8fafc;border:1px solid #e2e8f0;border-left:5px solid #64748b;border-radius:8px;padding:16px;margin:8px 0;">
<p style="margin:0 0 10px"><strong>🔧 Detalle técnico</strong></p>
<p style="margin:0 0 10px">Proyectos nuevos: <strong>AlmacenLite.Alertas</strong> y <strong>AlmacenLite.Reportes</strong>.</p>
<p style="margin:0">Ambos referencian <strong>AlmacenLite.DL</strong> y comparten el catálogo común.</p>
</div><p><br></p>
```

## Tablas

```html
<figure><table>
<thead>
<tr><th>Síntoma</th><th>Causa</th><th>Solución</th></tr></thead>
<tbody><tr><td>No aparece el mosaico</td><td>Faltan las filas en <strong>dbo.Widgets</strong></td><td>Ejecute el script de registro del módulo</td></tr><tr><td>Aparece encimado con otro widget</td><td>Comparte región con <strong>ConfirmModelChange</strong></td><td>Deshabilite uno de los dos en esa estación</td></tr></tbody>
</table></figure>
```

- Siempre `<figure><table>` + `<thead>` + `<tbody>`. `<th>`/`<td>` **sin atributos**: ni `align`, ni
  `style`, ni `colspan`. Lo que necesite énfasis va con `<strong>` dentro de la celda.
- Cabeceras habituales, en este orden de preferencia:
  `Síntoma | Causa | Solución` · `Mensaje | Causa | Solución` · `Tabla | Qué es` ·
  `Columna | Tipo | Descripción` · `Elemento | Tipo | GUID | Región` · `Situación | Qué hacer` ·
  `Campo | Para qué` · `Opción | Qué controla`. El estilo interrogativo (`Qué es`, `Para qué`,
  `Qué guarda`) es deliberado: la cabecera es la pregunta que responde la columna.
- Sin celdas vacías: escriba «Ninguna» o «—».

## Código

```html
<pre style="background: rgba(243, 244, 246, 1); border-radius: 6px; padding: 12px; overflow-x: auto"><code>INSERT INTO dbo.Widgets (Pk, Name, Description, RowIsActive, RowUpdateDate)
VALUES ('f8109b51-86c0-43f5-8ba9-08f3c4d17221', 'DateTime',
        'Muestra la fecha y hora actuales', 1, GETDATE());</code></pre>
```

- Siempre `<pre style="…"><code>`, sin clase de lenguaje (no hay resaltado en KnowledgeHub).
- `<`, `>` y `&` **escapados** dentro: `&lt;`, `&gt;`, `&amp;`. Un `<Project Sdk=…>` sin escapar es
  una etiqueta para el parser, y desaparece.
- El `<code>` en línea es `<code>` pelado: `<code>BottomRight</code>`.

## Imágenes

```html
<p style="text-align: center"><img src="data:image/png;base64,iVBORw0KGgo…" style="max-width: 100%"></p>
```

- Captura de pantalla o diagrama: párrafo centrado, `max-width: 100%`. Si la imagen es pequeña y
  no debe estirarse, tamaño explícito: `style="width: 177px; height: 83px"`.
- Icono dentro de una frase: `<img src="data:…" style="height: 1.25em; vertical-align: text-bottom">`
  (1.25em para iconos de botón, 1.4em–1.6em para mosaicos).
- Sin `alt` (el editor no lo genera y no se muestra en ningún sitio).
- Solo PNG, JPEG o WebP en base64. **Nunca** SVG en el `src` y **nunca** un archivo: ver
  `html-permitido.md`. Los diagramas se dibujan en SVG y se rasterizan con `scripts/embed_images.py`.
- Lo que no se puede capturar (la IA no ve la app) se deja como un **marcador visible** donde iría:

```html
<p style="text-align: center; color: #6b7280; border: 1px dashed #9ca3af; border-radius: 6px; padding: 12px; margin: 8px 0">[IMAGEN: pantalla completa de Part Deep Search con sus tres paneles]</p>
```

Visible a propósito: **un comentario HTML (`<!-- -->`) se borra al guardar** —medido con el saneador
real—, así que un marcador en comentario desaparece sin dejar rastro de qué imagen faltaba. El
párrafo punteado se ve en el editor y en el lector hasta que el autor lo sustituye por la captura
(se coloca el cursor en él, se inserta la imagen y se borra el texto). La lista de pendientes va
también al final de la generación.

## Pasos

Los pasos son **encabezados**, no listas: cada uno lleva su explicación, su captura y sus avisos.

```html
<h2 style="margin-top: 1.6em">Paso 1 · El Proyecto</h2>
<h3 style="margin-top: 1.2em">1. Presionar "INICIAR SESIÓN"</h3>
```

`Paso N · Título` (con punto medio) en `<h2>` para tutoriales largos; `N. Título` en `<h3>` para la
secuencia de pantallas de un manual.

`<ol>` se reserva para la **leyenda numerada de una captura**:

```html
<ol>
<li><strong>Título de la ventana</strong>: <strong>LINE CONTROL SYSTEM</strong>.</li>
<li><strong>Grupos de mosaicos</strong>: los módulos se organizan por área.</li>
</ol>
```

`<ul>` para enumeraciones sin orden (novedades de una versión, opciones, requisitos), con el nombre
de la cosa en `<strong>` al principio del `<li>` cuando lo tenga.

## Enlaces entre documentos

```html
<p>Documentación: <a href="04-SalidasPorOrden.html">Salidas por Orden de Trabajo</a> y
<a href="05-SalidasPorOrdenTecnica.html">su información técnica</a>.</p>

<p>Las dos tablas se cargan en memoria al arrancar. Vea <a href="09-ApiDelHost.html">La API del host</a>.</p>
```

- `href` relativo al archivo hermano, dentro de la misma carpeta. El texto del enlace es el título
  del destino. Se introduce con «Vea …» o «Documentación: …».
- Al pegar en KnowledgeHub esos `href` no resuelven: el autor los cambia por clic derecho sobre la
  página en el árbol → **Copiar enlace**. Por eso la generación termina con la lista de enlaces.
- Sin anclas `#seccion` (no hay `id` que las sostenga) y sin `mailto:`.

## Preguntas Frecuentes (cierre obligatorio)

```html
<h2 style="margin-top: 1.6em">Preguntas Frecuentes</h2>
<p><strong>P: ¿Esta versión requiere cambios en la base de datos?</strong>
R: Sí. Las tablas se crean con las migraciones del proyecto <strong>DbMigration</strong>. Vea <a href="15-Installation.html">Instalación</a>.</p>
<p><strong>P: Actualicé y no veo los mosaicos nuevos.</strong>
R: Se habilitan por usuario y por estación. Solicite el acceso al administrador.</p>
```

Un `<p>` por pregunta; `P:` en negrita, salto de línea real, `R:` en texto normal. Entre 3 y 6
preguntas, las que de verdad haría alguien que acaba de leer el documento.

## Errores Comunes (manuales y guías de instalación)

Justo antes de las FAQ, una tabla `Mensaje | Causa | Solución` (o `Síntoma | Causa | Solución`)
con lo que el usuario verá en pantalla, literal, en la primera columna.

## Portada / índice (`00-Index.html`)

Una tarjeta por área, con su icono, descripción y temas. Las clases `kh-mi-*` las viste el CSS del
anfitrión. Sin ese CSS, la misma información en `<h2>` por área + `<ul>` de enlaces.

```html
<h1>Manual de AlmacénLite</h1>
<p><strong>AlmacénLite</strong> es la aplicación de almacén de las estaciones de trabajo. Esta
librería reúne su manual, agrupado por el área que lo usa.</p>

<div class="kh-module-index">

  <article class="kh-mi-card">
    <div class="kh-mi-head">
      <div class="kh-mi-plate"><img src="data:image/png;base64,…"></div>
      <div class="kh-mi-heading">
        <h3>Producción</h3>
      </div>
    </div>
    <p class="kh-mi-desc">Módulos de operación, dirigidos a operadores y líderes.</p>
    <ul class="kh-mi-topics">
      <li><a href="04-SalidasPorOrden.html">Salidas por orden de trabajo</a></li>
      <li><a href="05-Inventario.html">Inventario y conteos</a></li>
    </ul>
    <a class="kh-mi-more" href="04-SalidasPorOrden.html">Ver todos los temas <span>→</span></a>
  </article>

</div>
```
