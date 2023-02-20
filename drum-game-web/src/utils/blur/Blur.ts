import { CompileShader, FrameBufferTexture, ShaderProgram } from "../GL";


const vertexShaderSource = `#version 300 es
in vec3 position;
out highp vec3 vPos;
out vec2 vUv;
void main() {
    gl_Position = vec4(position,1.0);
    vPos = position;
    vUv = position.xy / 2. + 0.5;
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
    vec2 sum = vec2(0.);
    
    for(float i=-rad;i<=rad;i++) {
        vec2 s = vec2(texture(uTexture,(vUv+vec2(i,0.)/800.)).w,1.);
        sum += gau(i)*s;
    }
    
    oFragColor = vec4(sum.x/sum.y);
}
`;

export function Blur(gl: WebGLRenderingContext, inputTexture: WebGLTexture) {
    const vertex = CompileShader(gl, vertexShaderSource, gl.VERTEX_SHADER);
    const frag2 = CompileShader(gl, fragShaderSource2, gl.FRAGMENT_SHADER);
    const frag3 = CompileShader(gl, fragShaderSource2.replace("vec2(i,0.)", "vec2(0.,i)"), gl.FRAGMENT_SHADER);

    const horizontalBlur = ShaderProgram(gl, vertex, frag2);
    const verticalBlur = ShaderProgram(gl, vertex, frag3);

    const tex = FrameBufferTexture(gl);
    const tex2 = FrameBufferTexture(gl);

    const fb = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, fb);

    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0)

    gl.useProgram(horizontalBlur);

    gl.bindTexture(gl.TEXTURE_2D, inputTexture);

    const posBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, posBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 0, 1, -1, 0, -1, 1, 0, 1, 1, 0]), gl.STATIC_DRAW);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(0);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);


    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex2, 0)
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.useProgram(verticalBlur);

    // gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    // gl.clear(gl.DEPTH_BUFFER_BIT);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

    // gl.bindFramebuffer(gl.FRAMEBUFFER, null);

    return tex2;
}