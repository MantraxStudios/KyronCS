#version 330 core

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

out vec4 FragColor;

uniform sampler2D u_Albedo;
uniform vec3 u_Color;

void main()
{
    vec4 texColor = texture(u_Albedo, TexCoord);

    vec3 color = u_Color == vec3(0.0) ? vec3(1.0) : u_Color;
    FragColor = vec4(texColor.rgb * color, texColor.a);
}
