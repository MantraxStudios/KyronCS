namespace KrayonCore.Graphics.GameUI
{
    internal static class UIShaders
    {
        public const string Vert = @"
#version 460 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;
out vec2 vUV;
uniform mat4  u_Projection;
uniform vec2  u_Position;
uniform vec2  u_Size;
uniform float u_Rotation;
void main()
{
    vec2 centered = aPos - vec2(0.5);
    float cosR = cos(u_Rotation);
    float sinR = sin(u_Rotation);
    vec2 rotated = vec2(
        centered.x * cosR - centered.y * sinR,
        centered.x * sinR + centered.y * cosR
    );
    vec2 worldPos = u_Position + (rotated + vec2(0.5)) * u_Size;
    gl_Position   = u_Projection * vec4(worldPos, 0.0, 1.0);
    vUV           = aUV;
}
";

        public const string Frag = @"
#version 460 core
in  vec2 vUV;
out vec4 FragColor;
uniform sampler2D u_Texture;
uniform vec4      u_Color;
uniform vec4      u_GradientColor;
uniform int       u_UseTexture;
uniform int       u_UseGradient;
uniform vec2      u_Size;
uniform float     u_CornerRadius;
float roundedRectSDF(vec2 p, vec2 halfSize, float r)
{
    vec2 q = abs(p) - halfSize + vec2(r);
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}
void main()
{
    vec2 pixelPos = (vUV - vec2(0.5)) * u_Size;
    float safeRadius = clamp(u_CornerRadius, 0.0, min(u_Size.x, u_Size.y) * 0.499);
    float dist = roundedRectSDF(pixelPos, u_Size * 0.5, safeRadius);
    float alpha = 1.0 - smoothstep(-0.5, 0.5, dist);
    if (alpha < 0.004) discard;
    vec4 baseColor;
    if (u_UseGradient == 1)
        baseColor = mix(u_Color, u_GradientColor, vUV.y);
    else
        baseColor = u_Color;
    if (u_UseTexture == 1)
        baseColor *= texture(u_Texture, vUV);
    FragColor = vec4(baseColor.rgb, baseColor.a * alpha);
}
";
    }
}