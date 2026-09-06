# Tipos de documento y sus secciones

Cada tipo tiene un esqueleto fijo. La rigidez es a propósito: quien lee un manual de módulo sabe
que la tabla de diagnóstico está siempre al final y que la referencia técnica de cualquier módulo
tiene las mismas seis secciones, y eso vale más que la creatividad. Elige el tipo por **quién lo va
a leer**, no por el tamaño del tema.

Todos empiezan por `<h1>` + un párrafo de presentación (qué es, para quién, en dos o tres frases)
y todos terminan en **Preguntas Frecuentes**. Los bloques exactos están en `plantillas.md`.

## Nombres de archivo y carpetas

- `NN-NombreEnPascal.html`, numerados desde `00-Index.html` en el orden de lectura. El número fija
  el orden en la carpeta. El título está dentro, en el `<h1>`.
- Una carpeta por audiencia (`Manual/` para usuarios, `DocTecnica/` para desarrolladores,
  `Plugins/` por familia) y **cada carpeta es un grafo cerrado**: los enlaces solo apuntan a
  hermanos. Lo que necesite el otro público se enlaza desde la portada de esa carpeta.
- Módulos: `NN-Mod-Nombre.html`. Versiones: `NN-Release-3.2.x.html`. Diagramas: `NN-ER-Esquema.html`.

## 1. Manual de usuario (módulo, plugin, pantalla)

Para operadores, líderes, administrativos. Cuenta qué hace la pantalla y cómo se usa, en el orden
en que el usuario la recorre.

```
<h1>Nombre del Módulo</h1>
<p>Qué es y para quién.</p>
[Consejos: versión a la que corresponde]
Quién Puede Usarlo            permisos y perfiles, en prosa
Pantalla Principal            captura (o marcador [IMAGEN: …]) + <ol> con la leyenda numerada
Cómo …                        una h2 por tarea, con los pasos como <h3>N. Título</h3>, cada uno con
                              su captura y sus avisos
Casos Particulares            lo que no es el camino feliz (opcional)
Errores Comunes               tabla Mensaje | Causa | Solución
Preguntas Frecuentes
```

Tono: usted, «presione», sin jerga de desarrollo. Las capturas que la IA no puede tomar se dejan
con el marcador visible `[IMAGEN: …]` de `plantillas.md`.

## 2. Referencia técnica de módulo

Para quien instala, configura o mantiene. Es la plantilla de mayor consistencia: **las seis
secciones, siempre, aunque la respuesta sea «Ninguna»**.

```
<h1>Módulo · Nombre</h1>
<p>Qué es en una frase, y qué lo hace especial (el más pequeño, el único que…).</p>
Registro          tabla Elemento | Tipo | GUID | Región (o Menú) + el SQL de alta en <pre><code>
Configuración     claves de ajustes, tablas que lee, constantes. Si nada: «<strong>Ninguna.</strong>» + por qué
Permisos          qué permisos exige y dónde se conceden. Si nada: «Ninguno.»
Depende de        proyectos/paquetes que referencia y por qué
Diagnóstico       tabla Síntoma | Causa
Preguntas Frecuentes
```

Un `❗ Importante` cuando el módulo comparte algo con otro (región, tabla, permiso) y el orden o el
conflicto no está definido.

## 3. Tutorial paso a paso

Para hacer algo una vez, de principio a fin: crear una extensión, montar una estación, migrar.

```
<h1>Crear una Extensión</h1>
<p>Objetivo y resultado final. Prerrequisitos en una lista.</p>
Paso 1 · Título     h2; qué se hace, el código o la captura, y el aviso si el paso no es opcional
Paso 2 · Título
…
Las Otras Variantes         si el tutorial tiene ramas (opcional)
Lista de Verificación       <ul> con lo que debe cumplirse al terminar
Preguntas Frecuentes
```

Cada paso termina en un estado comprobable («al arrancar, el mosaico aparece en el grupo…»).

## 4. Notas de versión

Una página por versión mayor (`Versión 3.2.X.X`). Las menores se acumulan dentro.

```
<h1>Versión 3.2.X.X</h1>
<p>Qué trae esta versión en dos frases. Enlace a la presentación de novedades si existe.</p>
General                       <ul> de cambios transversales
Nombre del Módulo             una h2 por módulo tocado, <ul> de cambios, y al final
                              «Documentación: <a>…</a>» hacia su manual
…
Preguntas Frecuentes          ¿requiere cambios en la base?, ¿por qué no veo lo nuevo?,
                              ¿dónde están las versiones anteriores?
```

Los cambios se describen por su efecto para el usuario («ahora se pueden autodescubrir los
parámetros con un clic»), no por el commit.

## 5. Portada / índice (`00-Index.html`)

La entrada de una carpeta. Una tarjeta por área con icono, descripción de una frase y sus temas
(bloque `kh-module-index` de `plantillas.md`). Sin FAQ: es el único tipo que no las lleva.

## 6. Modelo de datos (ER)

Un documento por esquema o por familia de tablas. Diagrama + explicación, nunca solo el diagrama.

```
<h1>Esquema Common</h1>
<p>Qué guarda este esquema y quién lo usa.</p>
Nombre del Grupo              h2 por grupo de tablas relacionadas:
                                diagrama ER (PNG embebido, fuente en diagramas/*.svg)
                                tabla Tabla | Qué es
                                tabla Columna | Tipo | Descripción para las tablas clave
…
Bases Externas                si lee de otras bases (opcional)
Preguntas Frecuentes
```

Las convenciones del dibujo están en `diagramas.md`. Un grupo pequeño (dos o tres tablas sin
relaciones que valga la pena ver) va solo con tabla, sin diagrama.

## 7. Arquitectura

Para el desarrollador que llega nuevo. Explica el diseño y **las decisiones que lo explican**, que
es lo que ningún código cuenta.

```
<h1>Arquitectura</h1>
<p>La idea central en un párrafo («no es una aplicación monolítica: es un host de complementos»).</p>
<ul> con las dos o tres propiedades que se derivan de esa idea
diagrama de capas / componentes
Las Capas                     tabla Capa | Proyecto | Responsabilidad
Quién Puede Llamar a Quién    la regla de dependencias, en prosa
El Arranque / El Descubrimiento   la secuencia, con código si aporta
Las Decisiones que Explican el Diseño   una h3 por decisión: qué se eligió, qué se descartó, por qué
Preguntas Frecuentes
```

## 8. Instalación y puesta en marcha

```
<h1>Instalación</h1>
Tecnologías / Requisitos
Estructura del Proyecto       árbol en <pre><code>
Instalar la Aplicación        pasos numerados con <h3>
Configuración Inicial         tablas de parámetros
Errores Comunes               tabla Mensaje | Causa | Solución
Preguntas Frecuentes
```

## Cómo elegir cuando el tema es grande

Un módulo con interfaz de usuario **y** configuración técnica son **dos documentos**: el manual (en
la carpeta del usuario) y la referencia técnica (en la técnica), enlazados desde sus portadas. No se
mezclan públicos en un archivo: el operador no debe leer SQL para encontrar cómo desbloquear su
estación.
