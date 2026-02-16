#version 330 core

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 EmissionColor;
layout(location = 2) out vec3 PositionOutput;
layout(location = 3) out vec3 NormalOutput;

in vec2 TexCoord;

uniform vec3 u_AlbedoColor;
uniform sampler2D u_AlbedoMap;
uniform int u_UseAlbedoMap;
uniform float u_Alpha;

void main()
{
    vec3 color;
    float alpha = 1.0;
    
    // Obtener color de la textura o usar color uniforme
    if(u_UseAlbedoMap == 1) {
        vec4 texColor = texture(u_AlbedoMap, TexCoord);
        color = pow(texColor.rgb, vec3(2.2)); // Gamma correction
        alpha = texColor.a;
    } else {
        color = u_AlbedoColor;
    }
    
    // Aplicar alpha uniforme
    alpha *= u_Alpha;
    
    // Tone mapping simple
    color = color / (color + vec3(1.0));
    
    // Gamma correction final
    color = pow(color, vec3(1.0/2.2));
    
    // Outputs
    FragColor = vec4(color, alpha);
    EmissionColor = vec4(0.0, 0.0, 0.0, alpha);
    PositionOutput = vec3(0.0);
    NormalOutput = vec3(0.0, 0.0, 1.0);
}
