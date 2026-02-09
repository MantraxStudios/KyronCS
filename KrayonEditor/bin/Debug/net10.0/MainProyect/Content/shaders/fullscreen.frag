#version 330 core

in vec2 v_TexCoord;
out vec4 FragColor;

uniform sampler2D u_ScreenTexture;
uniform vec2 u_Resolution;
uniform float u_Time;

uniform int u_PostProcessEnabled;

uniform int u_ColorCorrectionEnabled;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Saturation;
uniform vec3 u_ColorFilter;

uniform int u_BloomEnabled;
uniform float u_BloomThreshold;
uniform float u_BloomSoftThreshold;
uniform float u_BloomIntensity;
uniform float u_BloomRadius;

uniform int u_GrainEnabled;
uniform float u_GrainIntensity;
uniform float u_GrainSize;

float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

float getLuminance(vec3 color) {
    return dot(color, vec3(0.299, 0.587, 0.114));
}

vec3 prefilterBloom(vec3 color) {
    float brightness = getLuminance(color);
    float knee = u_BloomThreshold * u_BloomSoftThreshold;
    
    float soft = brightness - u_BloomThreshold + knee;
    soft = clamp(soft, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 0.00001);
    
    float contribution = max(soft, brightness - u_BloomThreshold);
    contribution /= max(brightness, 0.00001);
    
    return color * contribution;
}

vec3 gaussianBlur(sampler2D tex, vec2 uv, vec2 direction) {
    vec2 texelSize = 1.0 / u_Resolution;
    vec3 result = vec3(0.0);
    
    float weights[5];
    weights[0] = 0.227027;
    weights[1] = 0.1945946;
    weights[2] = 0.1216216;
    weights[3] = 0.054054;
    weights[4] = 0.016216;
    
    result += texture(tex, uv).rgb * weights[0];
    
    for(int i = 1; i < 5; i++) {
        vec2 offset = direction * texelSize * float(i) * u_BloomRadius;
        result += texture(tex, uv + offset).rgb * weights[i];
        result += texture(tex, uv - offset).rgb * weights[i];
    }
    
    return result;
}

vec3 getBloom(vec2 uv) {
    vec3 bloom = vec3(0.0);
    vec2 texelSize = 1.0 / u_Resolution;
    
    const int samples = 6;
    for(int x = -samples; x <= samples; x++) {
        for(int y = -samples; y <= samples; y++) {
            vec2 offset = vec2(float(x), float(y)) * texelSize * u_BloomRadius;
            vec3 sample = texture(u_ScreenTexture, uv + offset).rgb;
            
            vec3 bright = prefilterBloom(sample);
            
            float dist = length(vec2(x, y)) / float(samples);
            float weight = exp(-dist * dist * 2.0);
            
            bloom += bright * weight;
        }
    }
    
    return bloom / float((samples * 2 + 1) * (samples * 2 + 1));
}

vec3 applyColorCorrection(vec3 color) {
    color += u_Brightness;
    color = (color - 0.5) * u_Contrast + 0.5;
    
    float luma = getLuminance(color);
    color = mix(vec3(luma), color, u_Saturation);
    
    color *= u_ColorFilter;
    
    return color;
}

vec3 applyGrain(vec3 color, vec2 uv) {
    vec2 grainUV = uv * u_Resolution / u_GrainSize;
    float noise = random(grainUV + u_Time);
    noise = (noise - 0.5) * u_GrainIntensity;
    
    return color + noise;
}

void main()
{
    vec3 color = texture(u_ScreenTexture, v_TexCoord).rgb;
    
    if (u_PostProcessEnabled == 1) {
        if (u_BloomEnabled == 1) {
            vec3 bloom = getBloom(v_TexCoord);
            color = color + (bloom * u_BloomIntensity);
        }
        
        if (u_ColorCorrectionEnabled == 1) {
            color = applyColorCorrection(color);
        }
        
        if (u_GrainEnabled == 1) {
            color = applyGrain(color, v_TexCoord);
        }
    }
    
    FragColor = vec4(color, 1.0);
}