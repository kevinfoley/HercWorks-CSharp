#version 330 core

// The sky pass: a single full-viewport triangle built from gl_VertexID alone, so it needs no vertex
// buffer — only a bound (empty) VAO, which core-profile GL still requires.
//
// The fragment stage bands the sky by distance above the horizon in pixels, which is how the
// original's raster sky is built — see Content.SkyGradient. uHorizonY is the horizon's own window y,
// so the gradient rides the camera's pitch instead of being pinned to the middle of the view.
//
// Both stages live in this file, selected by VERTEX_SHADER / FRAGMENT_SHADER, which
// ShaderProgram.Load defines when it compiles each one. See Gl/ShaderSource.

#ifdef VERTEX_SHADER

void main() {
	// (-1,-1), (3,-1), (-1,3): one oversized triangle covering the whole clip rect.
	vec2 corner = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
	gl_Position = vec4(corner * 2.0 - 1.0, 0.0, 1.0);
}

#endif

#ifdef FRAGMENT_SHADER

uniform vec3 uBands[16];
uniform float uHorizonY;
uniform float uBandHeight;

out vec4 FragColor;

void main() {
	// Band 0 sits on the horizon and they climb from there; below it, the bottom band, which
	// terrain covers anyway except where the view looks down past the far edge of the world.
	float above = (gl_FragCoord.y - uHorizonY) / uBandHeight;
	int band = int(clamp(floor(above), 0.0, 15.0));

	// uBands is ordered zenith-first, so the horizon is the last entry and bands count back.
	FragColor = vec4(uBands[15 - band], 1.0);
}

#endif
