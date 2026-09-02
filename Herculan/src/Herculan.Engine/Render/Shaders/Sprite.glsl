#version 330 core

// The billboard pass: SpriteQuad corners already brought into view space by SpriteRenderer, which
// builds them there so that perspective reproduces the original's 1/depth scaling for free. All this
// stage does is apply the projection.
//
// Both stages live in this file, selected by VERTEX_SHADER / FRAGMENT_SHADER, which
// ShaderProgram.Load defines when it compiles each one. See Gl/ShaderSource.

#if defined(VERTEX_SHADER)
	#define VARYING out
#else
	#define VARYING in
#endif

// Declared once for both stages, so the two can never drift out of step.
VARYING vec2 vUV;

#ifdef VERTEX_SHADER

layout (location = 0) in vec3 aViewPosition;
layout (location = 1) in vec2 aUV;

uniform mat4 uProjection;

void main() {
	vUV = aUV;
	gl_Position = uProjection * vec4(aViewPosition, 1.0);
}

#endif

#ifdef FRAGMENT_SHADER

uniform sampler2D uTexture;

out vec4 FragColor;

void main() {
	// Palette index 0 decoded to alpha 0; the original's span routine skips that index rather
	// than blending it, so this is a test and not a blend.
	vec4 texel = texture(uTexture, vUV);
	if (texel.a < 0.5) {
		discard;
	}

	FragColor = vec4(texel.rgb, 1.0);
}

#endif
