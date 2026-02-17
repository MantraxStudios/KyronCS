#version 330 core

#include "includes/ssao.glsl"

out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D u_ScreenTexture;
uniform sampler2D u_EmissionTexture;
uniform sampler2D u_PositionTexture;
uniform sampler2D u_NormalTexture;
uniform sampler2D u_NoiseTexture;

uniform vec3  u_SSAOKernel[64];
uniform int   u_SSAOKernelSize;
uniform float u_SSAORadius;
uniform float u_SSAOBias;
uniform float u_SSAOPower;
uniform int   u_SSAOEnabled;

uniform mat4  u_Projection;
uniform mat4  u_View;
uniform vec2  u_Resolution;
uniform float u_Time;

uniform int   u_PostProcessEnabled;
uniform int   u_ColorCorrectionEnabled;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Saturation;
uniform vec3  u_ColorFilter;

uniform int   u_BloomEnabled;
uniform float u_BloomThreshold;
uniform float u_BloomSoftThreshold;
uniform float u_BloomIntensity;
uniform float u_BloomRadius;

uniform int   u_GrainEnabled;
uniform float u_GrainIntensity;
uniform float u_GrainSize;

float rand(vec2 co)
{
    return fract(sin(dot(co, vec2(127.1, 311.7))) * 43758.5453123);
}

float luma(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

vec3 ApplyColorCorrection(vec3 color)
{
    color *= max(u_Brightness, 0.0);
    color  = (color - 0.5) * max(u_Contrast, 0.0) + 0.5;
    float gray = luma(color);
    color  = mix(vec3(gray), color, max(u_Saturation, 0.0));
    color *= u_ColorFilter;
    return max(color, vec3(0.0));
}

vec3 QuadraticThreshold(vec3 color, float threshold, float knee)
{
    float lum = luma(color);
    float rq  = clamp(lum - threshold + knee, 0.0, 2.0 * knee);
    rq = (rq * rq) / (4.0 * knee + 0.00001);
    float weight = max(rq, lum - threshold) / max(lum, 0.00001);
    return color * weight;
}

vec3 ApplyBloom(vec2 uv)
{
    float threshold = u_BloomThreshold;
    float knee      = max(u_BloomSoftThreshold, 0.001);
    vec2  ts        = (1.0 / u_Resolution) * max(u_BloomRadius, 0.5);
    vec2  ts2       = ts * 2.0;
    vec2  ts3       = ts * 3.0;

    vec3 b0  = texture(u_EmissionTexture, uv).rgb;
    vec3 b1  = texture(u_EmissionTexture, uv + vec2(-ts2.x,   0.0  )).rgb;
    vec3 b2  = texture(u_EmissionTexture, uv + vec2( ts2.x,   0.0  )).rgb;
    vec3 b3  = texture(u_EmissionTexture, uv + vec2(  0.0,  -ts2.y )).rgb;
    vec3 b4  = texture(u_EmissionTexture, uv + vec2(  0.0,   ts2.y )).rgb;
    vec3 b5  = texture(u_EmissionTexture, uv + vec2(-ts.x,  -ts.y  )).rgb;
    vec3 b6  = texture(u_EmissionTexture, uv + vec2( ts.x,  -ts.y  )).rgb;
    vec3 b7  = texture(u_EmissionTexture, uv + vec2(-ts.x,   ts.y  )).rgb;
    vec3 b8  = texture(u_EmissionTexture, uv + vec2( ts.x,   ts.y  )).rgb;
    vec3 b9  = texture(u_EmissionTexture, uv + vec2(-ts2.x, -ts2.y )).rgb;
    vec3 b10 = texture(u_EmissionTexture, uv + vec2( ts2.x, -ts2.y )).rgb;
    vec3 b11 = texture(u_EmissionTexture, uv + vec2(-ts2.x,  ts2.y )).rgb;
    vec3 b12 = texture(u_EmissionTexture, uv + vec2( ts2.x,  ts2.y )).rgb;

    vec3 bloom = vec3(0.0);
    bloom += QuadraticThreshold(b0,  threshold, knee) * 0.125;
    bloom += QuadraticThreshold(b1,  threshold, knee) * 0.0625;
    bloom += QuadraticThreshold(b2,  threshold, knee) * 0.0625;
    bloom += QuadraticThreshold(b3,  threshold, knee) * 0.0625;
    bloom += QuadraticThreshold(b4,  threshold, knee) * 0.0625;
    bloom += QuadraticThreshold(b5,  threshold, knee) * 0.125;
    bloom += QuadraticThreshold(b6,  threshold, knee) * 0.125;
    bloom += QuadraticThreshold(b7,  threshold, knee) * 0.125;
    bloom += QuadraticThreshold(b8,  threshold, knee) * 0.125;
    bloom += QuadraticThreshold(b9,  threshold, knee) * 0.03125;
    bloom += QuadraticThreshold(b10, threshold, knee) * 0.03125;
    bloom += QuadraticThreshold(b11, threshold, knee) * 0.03125;
    bloom += QuadraticThreshold(b12, threshold, knee) * 0.03125;

    vec3 sp0 = texture(u_EmissionTexture, uv + vec2(-ts3.x, -ts3.y)).rgb;
    vec3 sp1 = texture(u_EmissionTexture, uv + vec2( ts3.x, -ts3.y)).rgb;
    vec3 sp2 = texture(u_EmissionTexture, uv + vec2(-ts3.x,  ts3.y)).rgb;
    vec3 sp3 = texture(u_EmissionTexture, uv + vec2( ts3.x,  ts3.y)).rgb;
    vec3 sp4 = texture(u_EmissionTexture, uv + vec2(-ts3.x,   0.0 )).rgb;
    vec3 sp5 = texture(u_EmissionTexture, uv + vec2( ts3.x,   0.0 )).rgb;
    vec3 sp6 = texture(u_EmissionTexture, uv + vec2(  0.0,  -ts3.y)).rgb;
    vec3 sp7 = texture(u_EmissionTexture, uv + vec2(  0.0,   ts3.y)).rgb;

    vec3 spread = (sp0 + sp1 + sp2 + sp3 + sp4 + sp5 + sp6 + sp7) * 0.125;
    float spreadLum = luma(spread);
    float spreadW   = max(spreadLum - threshold, 0.0) / max(spreadLum, 0.00001);
    spread *= spreadW;

    bloom += spread * 0.25;

    return bloom * u_BloomIntensity;
}

vec3 ApplyGrain(vec3 color, vec2 uv)
{
    float frame    = floor(u_Time * 24.0);
    vec2  grainUV  = floor(uv * u_Resolution / max(u_GrainSize, 0.5));
    float noise    = rand(grainUV + vec2(frame * 0.8143, frame * 0.5891)) * 2.0 - 1.0;
    float lumResp  = sqrt(max(luma(color) - luma(color) * luma(color), 0.0)) * 3.5;
    return color + noise * u_GrainIntensity * lumResp;
}

void main()
{
    vec3 color = texture(u_ScreenTexture, TexCoord).rgb;

    if (u_PostProcessEnabled == 1)
    {
        if (u_SSAOEnabled == 1)
        {
            float ao = CalculateSSAO(
                TexCoord,
                u_PositionTexture,
                u_NormalTexture,
                u_NoiseTexture,
                u_SSAOKernel,
                u_SSAOKernelSize,
                u_SSAORadius,
                u_SSAOBias,
                u_Projection,
                u_Resolution
            );
            ao     = pow(clamp(ao, 0.0, 1.0), u_SSAOPower);
            color *= ao;
        }

        if (u_BloomEnabled == 1)
        {
            vec3 bloom = ApplyBloom(TexCoord);
            color += bloom;
        }

        if (u_ColorCorrectionEnabled == 1)
        {
            color = ApplyColorCorrection(color);
        }

        if (u_GrainEnabled == 1)
        {
            color = ApplyGrain(color, TexCoord);
        }
    }

    FragColor = vec4(color, 1.0);
}