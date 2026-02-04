#version 330 core

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

out vec4 FragColor;

uniform sampler2D mainTexture;
uniform vec3 u_Color;

void main()
{
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(vec3(0.5, 1.0, 0.3));
    
    // Diffuse lighting
    float diff = max(dot(norm, lightDir), 0.0);
    
    // Sample texture
    vec4 texColor = texture(mainTexture, TexCoord);
    
    // Combine texture with color and lighting
    vec3 diffuse = diff * texColor.rgb * u_Color;
    vec3 ambient = 0.3 * texColor.rgb * u_Color;
    
    vec3 result = ambient + diffuse;
    FragColor = vec4(result, texColor.a);
}