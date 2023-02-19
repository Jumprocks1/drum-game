import { CompileShader, ShaderProgram } from "../GL";


const vertexShaderSource = `#version 300 es
in vec3 position;
in vec2 uv;
out highp vec3 vPos;
out vec2 vUv;
void main() {
    gl_Position = vec4(position,1.0);
    vPos = position;
    vUv = uv;
}
`;
const fragShaderSource = `#version 300 es
precision highp float;
in vec3 vPos;
in vec2 vUv;

uniform sampler2D uTexture;

out vec4 oFragColor;
void main(void) {
    if (vPos.z >= 0.)
        oFragColor = vec4(1.,1.,1.,1.);
    else
        oFragColor = vec4(0.,0.,0.,1.);
}
`;
const fragShaderSource2 = `#version 300 es
precision highp float;

const float rad = 20.;
const float dev = 20.;
const float m = 0.398942280401/dev;
float gau(float x) {return m*exp(-x*x*0.5/(dev*dev));}

in vec3 vPos;
in vec2 vUv;

uniform sampler2D uTexture;

out vec4 oFragColor;
void main(void) {
    vec4 sum = vec4(0.);
    
    for(float i=-rad;i<=rad;i++) sum += gau(i)*texture(uTexture,(vUv+vec2(i,0.)/800.));
    
    oFragColor = vec4(sum.rgb/sum.a,1.);
}
`;

export function Blur(gl: WebGLRenderingContext, draw: () => void) {
    const vertex = CompileShader(gl, vertexShaderSource, gl.VERTEX_SHADER);
    const frag = CompileShader(gl, fragShaderSource, gl.FRAGMENT_SHADER);
    const frag2 = CompileShader(gl, fragShaderSource2, gl.FRAGMENT_SHADER);
    const frag3 = CompileShader(gl, fragShaderSource2.replace("vec2(i,0.)", "vec2(0.,i)"), gl.FRAGMENT_SHADER);

    const program = ShaderProgram(gl, vertex, frag);
    const horizontalBlur = ShaderProgram(gl, vertex, frag2);
    const verticalBlur = ShaderProgram(gl, vertex, frag3);

    const fb = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, fb);

    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.canvas.width, gl.canvas.height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0)

    gl.useProgram(program);

    draw();

    const tex2 = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex2);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.canvas.width, gl.canvas.height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex2, 0)

    gl.useProgram(horizontalBlur);

    gl.bindTexture(gl.TEXTURE_2D, tex);

    const posBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, posBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 0, 1, -1, 0, -1, 1, 0, 1, 1, 0]), gl.STATIC_DRAW);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(0);

    const uvBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, uvBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]), gl.STATIC_DRAW);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(1);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0)
    gl.bindTexture(gl.TEXTURE_2D, tex2);
    gl.useProgram(verticalBlur);


    // gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    // gl.clear(gl.DEPTH_BUFFER_BIT);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);

    return tex;
}