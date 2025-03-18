import asyncio
import websockets
import json
import base64
import wave
import numpy as np

buffer = []
save_count = 0
async def process_audio_data():
    global buffer
    global save_count
    if len(buffer) >= 50:
        print("Processing audio data...")
        # 组合缓冲区中的数据
        audio_data = b''.join(buffer)

        # 存储一个buffer 测试
        with open(f'data/output{save_count}.pcm', 'wb') as f:
            f.write(audio_data)

        buffer = []  # 清空缓冲区

        # 将 PCM 数据写入 WAV 文件
        with wave.open(f'data/output{save_count}.wav', 'wb') as wf:
            wf.setnchannels(1)  # 单声道
            wf.setsampwidth(2)  # 16位
            wf.setframerate(22500)  # 22500 Hz
            wf.writeframes(audio_data)
            save_count += 1
        print("Audio data saved to output.wav")

async def handler(websocket):
    global buffer
    print("Client connected")
    
    async def ping():
        while True:
            try:
                await websocket.send(json.dumps({"type": "ping"}))
                await asyncio.sleep(10)  # 每10秒发送一次ping
            except websockets.ConnectionClosed:
                break

    asyncio.create_task(ping())

    async for message in websocket:
        # 记录接收到的数据到文件
        with open('data/received_data.txt', 'a') as f:
            f.write(message + '\n')
        data = json.loads(message)
        if data['type'] == 'input_audio_buffer.append':
            print("Received audio buffer data")
            pcm_data = base64.b64decode(data['audio'])
            buffer.append(pcm_data)
            await process_audio_data()
    print("Client disconnected")

async def main():
    print("Starting server...")
    async with websockets.serve(handler, "0.0.0.0", 8765):
        print("Server started on ws://0.0.0.0:8765")
        await asyncio.Future()  # run forever

if __name__ == "__main__":
    asyncio.run(main())
