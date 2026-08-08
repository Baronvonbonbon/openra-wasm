/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 *
 * Bridges managed code to Emscripten's WebGL emulation. .NET resolves wasm
 * P/Invokes through a generated table rather than by shared library name, so
 * the Emscripten and GL entry points are reached through this shim, which is
 * linked into the runtime via NativeFileReference.
 */

#include <emscripten/html5.h>
#include <GLES3/gl3.h>
#include <string.h>

/* Creates a WebGL2 context on the given canvas selector and makes it current.
   Returns the context handle, or 0 on failure. */
int openra_gl_create_context(const char *selector)
{
	EmscriptenWebGLContextAttributes attributes;
	emscripten_webgl_init_context_attributes(&attributes);

	/* WebGL2 == OpenGL ES 3.0, which is what GLProfile.Embedded targets. */
	attributes.majorVersion = 2;
	attributes.minorVersion = 0;
	attributes.alpha = 0;
	attributes.depth = 1;
	attributes.stencil = 0;
	attributes.antialias = 0;
	attributes.preserveDrawingBuffer = 0;
	attributes.enableExtensionsByDefault = 1;

	EMSCRIPTEN_WEBGL_CONTEXT_HANDLE context = emscripten_webgl_create_context(selector, &attributes);
	if (context <= 0)
		return 0;

	if (emscripten_webgl_make_context_current(context) != EMSCRIPTEN_RESULT_SUCCESS)
		return 0;

	return (int)context;
}

const char *openra_gl_get_string(unsigned int name)
{
	return (const char *)glGetString(name);
}

void openra_gl_clear_color(float r, float g, float b, float a)
{
	glClearColor(r, g, b, a);
}

void openra_gl_clear(unsigned int mask)
{
	glClear(mask);
}

void openra_gl_read_pixel(int x, int y, unsigned char *out_rgba)
{
	glReadPixels(x, y, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, out_rgba);
}

/* Returns a callable function pointer for a GL entry point, so managed code can
   bind GL through delegates exactly the way the desktop build binds them through
   SDL_GL_GetProcAddress. Requires the getprocaddr-enabled GL library, which the
   .NET wasm runtime already links. */
void *openra_gl_get_proc_address(const char *name)
{
	return emscripten_webgl_get_proc_address(name);
}

int openra_gl_is_extension_supported(const char *name)
{
	/* GL spells extensions "GL_EXT_foo" while WebGL spells the same one "EXT_foo",
	   so both spellings are tried. */
	const char *unprefixed = name;
	if (strncmp(name, "GL_", 3) == 0)
		unprefixed = name + 3;

	EMSCRIPTEN_WEBGL_CONTEXT_HANDLE context = emscripten_webgl_get_current_context();
	if (emscripten_webgl_enable_extension(context, unprefixed))
		return 1;

	if (emscripten_webgl_enable_extension(context, name))
		return 1;

	const char *extensions = (const char *)glGetString(GL_EXTENSIONS);
	if (extensions == 0)
		return 0;

	for (int attempt = 0; attempt < 2; attempt++)
	{
		const char *needle = attempt == 0 ? name : unprefixed;
		const size_t length = strlen(needle);
		for (const char *p = extensions; (p = strstr(p, needle)) != 0; p += length)
		{
			const char after = p[length];
			if ((p == extensions || p[-1] == ' ') && (after == ' ' || after == '\0'))
				return 1;
		}
	}

	return 0;
}

/* Reports the driver's extension string, for diagnosing feature detection. */
const char *openra_gl_extensions(void)
{
	return (const char *)glGetString(GL_EXTENSIONS);
}

void openra_gl_destroy_context(int context)
{
	emscripten_webgl_destroy_context((EMSCRIPTEN_WEBGL_CONTEXT_HANDLE)context);
}

void openra_canvas_size(const char *selector, int *width, int *height)
{
	double w = 0, h = 0;
	emscripten_get_element_css_size(selector, &w, &h);
	*width = (int)w;
	*height = (int)h;
}

/* Trampoline anchors: never called, and exist only so the managed side has a
   DllImport per distinct GL delegate signature. See TrampolineAnchors.cs. */
void openra_trampoline_00(void * a0, void * a1)
{
}

void openra_trampoline_01(int a0, int a1, unsigned int a2, int a3, int a4, const char * a5)
{
}

void openra_trampoline_02(void)
{
}

void openra_trampoline_03(int a0, int a1, int a2, int a3)
{
}

void openra_trampoline_04(int a0)
{
}

void openra_trampoline_05(float a0, float a1, float a2, float a3)
{
}

int openra_trampoline_06(void)
{
	return 0;
}

void * openra_trampoline_07(int a0)
{
	return 0;
}

void * openra_trampoline_08(int a0, unsigned int a1)
{
	return 0;
}

void openra_trampoline_09(int a0, int * a1)
{
}

unsigned int openra_trampoline_10(void)
{
	return 0;
}

void openra_trampoline_11(unsigned int a0)
{
}

void openra_trampoline_12(unsigned int a0, int a1, int * a2)
{
}

unsigned int openra_trampoline_13(int a0)
{
	return 0;
}

void openra_trampoline_14(unsigned int a0, int a1, const char ** a2, void * a3)
{
}

void openra_trampoline_15(unsigned int a0, unsigned int a1)
{
}

void openra_trampoline_16(unsigned int a0, int a1, int * a2, char * a3)
{
}

int openra_trampoline_17(unsigned int a0, const char * a1)
{
	return 0;
}

void openra_trampoline_18(int a0, int a1)
{
}

void openra_trampoline_19(int a0, float a1)
{
}

void openra_trampoline_20(int a0, float a1, float a2)
{
}

void openra_trampoline_21(int a0, float a1, float a2, float a3)
{
}

void openra_trampoline_22(int a0, int a1, void * a2)
{
}

void openra_trampoline_23(int a0, int a1, int a2, void * a3)
{
}

void openra_trampoline_24(int a0, unsigned int * a1)
{
}

void openra_trampoline_25(int a0, unsigned int a1)
{
}

void openra_trampoline_26(int a0, void * a1, void * a2, int a3)
{
}

void openra_trampoline_27(int a0, void * a1, void * a2, void * a3)
{
}

void openra_trampoline_28(int a0, unsigned int * a1)
{
}

void openra_trampoline_29(unsigned int a0, int a1, const char * a2)
{
}

void openra_trampoline_30(int a0, int a1, int a2)
{
}

void openra_trampoline_31(int a0, int a1, int a2, void * a3)
{
}

int openra_trampoline_32(unsigned int a0)
{
	return 0;
}

void openra_trampoline_33(int a0, int a1, float a2)
{
}

int openra_trampoline_34(int a0)
{
	return 0;
}

/* FreeType here is built without the gzip module, which needs setjmp and does
   not link against the runtime's compiler-rt. Only WOFF fonts need it, and
   OpenRA ships plain TrueType, so this reports the feature as unavailable.
   FT_Err_Unimplemented_Feature is 0x94. */
int FT_Gzip_Uncompress(void *memory, unsigned char *output, unsigned long *output_len,
                       const unsigned char *input, unsigned long input_len)
{
	(void)memory; (void)output; (void)output_len; (void)input; (void)input_len;
	return 0x94;
}
