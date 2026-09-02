#version 330 core

// Flat-coloured line geometry — currently just the editor's selection box. See WireframeRenderer for
// why this has a program of its own rather than reusing the scene's.
//
// Both stages live in this file, selected by VERTEX_SHADER / FRAGMENT_SHADER, which
// ShaderProgram.Load defines when it compiles each one. See Gl/ShaderSource.

#ifdef VERTEX_SHADER

layout (location = 0) in vec3 aPosition;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

void main() {
	gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
}

#endif

#ifdef FRAGMENT_SHADER

uniform vec3 uColor;

out vec4 FragColor;

void main() {
	FragColor = vec4(uColor, 1.0);
}

#endif
