#version 330 core

out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D u_AlbedoMap;
uniform int u_UseAlbedoMap;
uniform vec3 u_AlbedoColor;

void main()
{
    vec4 texColor;
    
    if(u_UseAlbedoMap == 1) {
        texColor = texture(u_AlbedoMap, TexCoord);
    } else {
        texColor = vec4(u_AlbedoColor, 1.0);
    }
    
    if(texColor.a < 0.01) {
        discard;
    }
    
    FragColor = texColor;
}