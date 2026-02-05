#version 330 core

out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D u_AlbedoMap;
uniform vec3 u_AlbedoColor;
uniform int u_UseAlbedoMap;
uniform float u_Alpha;

void main()
{
    vec4 texColor;
    
    if(u_UseAlbedoMap == 1) {
        texColor = texture(u_AlbedoMap, TexCoord);
    } else {
        texColor = vec4(u_AlbedoColor, 1.0);
    }
    
    // Aplicar alpha si se especifica
    texColor.a *= u_Alpha;
    
    FragColor = texColor;
}
