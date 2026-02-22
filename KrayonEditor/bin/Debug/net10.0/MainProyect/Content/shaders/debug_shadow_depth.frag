#version 330 core

in vec2 TexCoord;

uniform sampler2D u_DepthTexture;

layout(location = 0) out vec4 FragColor;

void main()
{
    // Sample depth from the shadow map (RGBA format, depth is in .r channel)
    // Ahora el shadow map se limpia a 1.0 (max = sin sombra) y la geometría
    // escribe valores menores → mostrar directamente (oscuro = cerca/sombra, claro = lejos)
    float depth = texture(u_DepthTexture, TexCoord).r;
    // Aplicar pow para aumentar contraste en rango bajo
    float vis = pow(clamp(depth, 0.0, 1.0), 0.4);
    FragColor = vec4(vec3(vis), 1.0);
}
