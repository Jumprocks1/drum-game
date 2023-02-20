#version 300 es
precision highp float;


uniform float iTime;
uniform sampler2D lineTexture;
uniform sampler2D shadowTexture;

void mainImage(out vec4 fragColor);
in vec2 vUv;
out vec4 oFragColor;
void main(void) {
    vec4 fragColor;
    mainImage(fragColor);
    oFragColor = fragColor;
}

const float tau = 3.1415926535*2.0;
const float pi = 3.1415926535;
const float pi12 = 3.1415926535 / 2.;

const float root3 = 1.7320508;

const vec3 grey = vec3(0.161, 0.165, 0.176); // #292a2d
const vec3 blue = vec3(0.141, 0.447, 0.784); // #2472c8

const vec3 colors[4] = vec3[4](
    grey,
    grey * 0.85 + blue * 0.15,
    grey * 1.1,
    grey * 0.95
);
const float colorLen = float(colors.length());


float random (vec2 st) {
    return fract(sin(dot(st.xy,vec2(12.9898,78.233)))*43758.5453123);
}

vec2 getHex(vec2 p)
{
    float x = p.x / root3;
    float y = p.y;
    
    float t1 = y;
    float t2 = floor(x + y);
    float r = floor((floor(t1 - x) + t2) / 3.0);
    float q = floor((floor(2.0 * x + 1.0) + t2) / 3.0) - r;
    return vec2(q,r);
}

vec3 hexBgColor(vec2 uv) {
    vec3 color = vec3(0.);

    vec2 coord = getHex(uv*10.0 + iTime * vec2(0.5,2.0) + sin(iTime) * 0.3);
   
    float rand = random(coord) * colorLen;
    
    int colorIndex = int(rand);
    
    color += colors[0];
    
    color += (colors[colorIndex] - colors[0])*0.6;
    
    coord = getHex(uv.yx*4.0 + iTime * vec2(-0.2,0.6) + sin(iTime*0.2) * 2.0);
    colorIndex = int(mod(coord.x - coord.y, colorLen));
    
    rand = random(coord);
    
    colorIndex = int(rand * colorLen);
    color += (colors[colorIndex] - colors[0])*0.6;

    return color - 0.12;
}

vec2 radial_distort(vec2 uv) { // this is a fun one
    float t = iTime + length(uv) * 1.;
    float distortion = sin(t * tau) * 0.1; // can make this higher for more fun
    uv += normalize(uv) * distortion;
    return uv;
}
vec2 distort(vec2 uv) {
    float t = iTime + uv.x * 2.;
    float distortion = sin(t * tau) * 0.01;
    uv += vec2(0.,1.) * distortion;
    return uv;
}

void mainImage(out vec4 fragColor)
{
    vec2 uv = vUv;//distort(vUv);

    const float borderCenter = 0.95;

    const float baseMaxLighting = 1.7;
    const float targetWidth = .012; // width of maxed out lighting in the border
    const float bleedAmount = 1.2;

    vec3 color = vec3(0.);

    float r = length(uv);

    float ang = atan(uv.y,uv.x);

    // we divide by `r` so that the growth is more spread out in the center
    float growthPortion = 0.2 / pow(r / borderCenter, 3.); // proportion of half the circle, 1 => half the circle is modified

    float growthTime = iTime * 0.5;

    float growth = max((sin(ang + growthTime) - 1.0) / growthPortion + 1.0, 0.0);

    // growth = 0.;
   
    const float power = 1. / bleedAmount;
    const float baseLightingPower = pow(targetWidth, power) * baseMaxLighting;
    
    float lightingPower = baseLightingPower + growth * 0.05;

    {
        const float outerDecay = 10.; // helps prevent the cutoff on canvas borders
        const float outerDecayStart = borderCenter;
        lightingPower *= clamp(outerDecay + 1. - r / outerDecayStart * outerDecay, 0., 1.0);
    }

    float lightingDist = pow(abs(r - borderCenter), power);
    float maxLighting = baseMaxLighting + growth * 2.;

    float lightMod = min(lightingPower / lightingDist, maxLighting);
    
    color += blue * lightMod;

    float growthAngle = pi12 - growthTime;
    
    vec3 lineData = texture(lineTexture, (uv + 1.) / 2.).xyz;
    if (lineData.z > 0.) {
        // should maybe add a global north light so that even without the growth light we can still see normals

        lineData.xy = lineData.xy * 2. - 1.;
        vec2 dir = -lineData.xy;
        vec2 lightDir = normalize(-uv);

        float lightPower = 0.5; // global light power, regardless of distance

        lightPower += max(dot(dir, lightDir),0.) * baseLightingPower / lightingDist * 1.; // distance based with normals

        vec2 lightPos = borderCenter * vec2(cos(growthAngle),sin(growthAngle));

        float growthLightDist = pow(distance(uv, lightPos), power);

        lightPower += 1. * max(0., dot(normalize(uv - lightPos), dir.xy)) / growthLightDist;
        lightPower += 0.1 / growthLightDist; // distance based, ignores normals

        color += blue * lightPower;
    } else {
        if (r < borderCenter) {
            color += hexBgColor(uv) * min(maxLighting - lightMod, 1.);

            vec2 shadowOffset = vec2(cos(growthAngle), sin(growthAngle)) * 0.05;
            color *= 1. - (texture(shadowTexture, (uv + shadowOffset + 1.) / 2.).rgb);
        }
    }


    fragColor = vec4(color, 1.);
}
