<script setup lang="ts">
import { ref, reactive } from 'vue'
import { getJson, putJson, postJson, setLanguage } from './api'

type Tab = 'status' | 'config' | 'logs' | 'drivers' | 'service' | 'health' | 'pcsc' | 'transitcard' | 'printer' | 'ws'

const activeTab = ref<Tab>('status')
const tabs: { key: Tab; label: string }[] = [
  { key: 'status', label: 'STATUS' },
  { key: 'config', label: 'CONFIG' },
  { key: 'logs', label: 'LOGS' },
  { key: 'drivers', label: 'DRIVERS' },
  { key: 'service', label: 'SERVICE' },
  { key: 'health', label: 'HEALTH' },
  { key: 'pcsc', label: 'PCSC' },
  { key: 'transitcard', label: 'TRANSIT' },
  { key: 'printer', label: 'PRINTER' },
  { key: 'ws', label: 'WS' },
]

const lang = ref('en-US')
function changeLang(e: Event) {
  lang.value = (e.target as HTMLSelectElement).value
  setLanguage(lang.value)
}

const results = reactive<Record<string, string>>({})
const errors = reactive<Record<string, string>>({})

async function call(label: string, fn: () => Promise<unknown>) {
  errors[label] = ''
  results[label] = ''
  try {
    const data = await fn()
    results[label] = JSON.stringify(data, null, 2)
  } catch (e: unknown) {
    errors[label] = e instanceof Error ? e.message : String(e)
  }
}

// ==============================
// 1. Status
// ==============================
async function getStatus() {
  await call('status', () => getJson('/api/status'))
}

// ==============================
// 2. Config
// ==============================
const configPutBody = ref('')

async function getConfig() {
  await call('configGet', () => getJson('/api/config'))
}

async function putConfig() {
  if (!configPutBody.value) return
  await call('configPut', () => putJson('/api/config', JSON.parse(configPutBody.value)))
}

function autoFillConfig() {
  window.setTimeout(() => { configPutBody.value = results.configGet }, 200)
}

// ==============================
// 3. Logs
// ==============================
const logsLevel = ref('Information')
const logsTail = ref(50)
const logsQuery = ref('')

async function getLogs() {
  const params = new URLSearchParams({ level: logsLevel.value, tail: String(logsTail.value) })
  if (logsQuery.value) params.set('query', logsQuery.value)
  await call('logs', () => getJson(`/api/logs?${params}`))
}

// ==============================
// 4. Drivers
// ==============================
const driverList = ref<{ name: string; status: string; enabled: boolean }[]>([])

async function getDrivers() {
  await call('driversGet', async () => {
    const data = await getJson<{ name: string; status: string; enabled: boolean }[]>('/api/drivers')
    driverList.value = data
    return data
  })
}

async function enableDriver(name: string) {
  await call(`driversEnable_${name}`, () => postJson(`/api/drivers/${name}/enable`))
  await getDrivers()
}

async function disableDriver(name: string) {
  await call(`driversDisable_${name}`, () => postJson(`/api/drivers/${name}/disable`))
  await getDrivers()
}

// ==============================
// 5. Service
// ==============================
async function restartService() {
  await call('serviceRestart', () => postJson('/api/service/restart'))
}

// ==============================
// 6. Health
// ==============================
async function getHealth() {
  await call('health', () => getJson('/api/health'))
}

// ==============================
// Config Reset
// ==============================
async function resetConfig() {
  await call('configReset', () => postJson('/api/config/reset'))
}

// ==============================
// 7. PCSC
// ==============================
const pcscReaderName = ref('')
const pcscApdu = ref('00A4040008A000000003000000')

async function listReaders() {
  await call('pcscReaders', () => getJson('/api/hardware/pcsc/readers'))
}

async function getReaderInfo() {
  if (!pcscReaderName.value) return
  await call('pcscInfo', () => getJson(`/api/hardware/pcsc/readers/${encodeURIComponent(pcscReaderName.value)}`))
}

async function getAtr() {
  if (!pcscReaderName.value) return
  await call('pcscAtr', () => getJson(`/api/hardware/pcsc/readers/${encodeURIComponent(pcscReaderName.value)}/atr`))
}

async function transmit() {
  if (!pcscReaderName.value || !pcscApdu.value) return
  await call('pcscTransmit', () => postJson(
    `/api/hardware/pcsc/transmit`,
    { readerName: pcscReaderName.value, apdu: pcscApdu.value }))
}

// ==============================
// 8. Transit Card
// ==============================
const transitReaderName = ref('')
const transitRechargeAmount = ref(5000)
const transitSessionId = ref('')
const transitMacSignature = ref('AABBCCDD')

async function getTransitReaders() {
  await call('transitReaders', () => getJson('/api/hardware/transitcard/readers'))
}

async function getCardInfo() {
  const params = transitReaderName.value ? `?readerName=${encodeURIComponent(transitReaderName.value)}` : ''
  await call('transitInfo', () => getJson(`/api/hardware/transitcard/info${params}`))
}

async function getBalance() {
  const params = transitReaderName.value ? `?readerName=${encodeURIComponent(transitReaderName.value)}` : ''
  await call('transitBalance', () => getJson(`/api/hardware/transitcard/balance${params}`))
}

async function getTransactions() {
  const params = new URLSearchParams({ count: '5' })
  if (transitReaderName.value) params.set('readerName', transitReaderName.value)
  await call('transitTx', () => getJson(`/api/hardware/transitcard/transactions?${params}`))
}

async function rechargeInit() {
  const body: Record<string, unknown> = { amount: transitRechargeAmount.value }
  if (transitReaderName.value) body.readerName = transitReaderName.value
  await call('rechargeInit', () => postJson('/api/hardware/transitcard/recharge/init', body))
}

async function rechargeExecute() {
  if (!transitSessionId.value) return
  await call('rechargeExec', () => postJson('/api/hardware/transitcard/recharge/execute', {
    sessionId: transitSessionId.value,
    macSignature: transitMacSignature.value
  }))
}

// ==============================
// 9. Printer
// ==============================
const printerName = ref('')
const printerText = ref('')
const printerNameRaw = ref('')
const printerRawData = ref('')

async function getPrinters() {
  await call('printerList', () => getJson('/api/hardware/printer/printers'))
}

async function printText() {
  if (!printerText.value) return
  await call('printerPrint', () => postJson('/api/hardware/printer/print', {
    text: printerText.value,
    printerName: printerName.value || undefined
  }))
}

async function printRaw() {
  if (!printerRawData.value) return
  await call('printerRaw', () => postJson('/api/hardware/printer/print-raw', {
    data: printerRawData.value,
    printerName: printerNameRaw.value || undefined
  }))
}

// ==============================
// 10. WebSocket
// ==============================
const wsConnected = ref(false)
const wsLog = ref<string[]>([])
const wsTarget = ref('pcsc')
const wsAction = ref('list_readers')
const wsParams = ref('{}')
let ws: WebSocket | null = null

function wsConnect() {
  if (ws) ws.close()
  ws = new WebSocket(`ws://${location.host}/ws`)
  ws.onopen = () => { wsConnected.value = true; wsLog.value.unshift('[连接] 已建立') }
  ws.onclose = (e) => { wsConnected.value = false; wsLog.value.unshift(`[断开] code=${e.code}`) }
  ws.onmessage = (e) => {
    let text = e.data
    try { text = JSON.stringify(JSON.parse(e.data), null, 2) } catch { }
    wsLog.value.unshift(`[收到] ${text}`)
  }
  ws.onerror = () => wsLog.value.unshift('[错误] 连接异常')
}

function wsDisconnect() {
  ws?.close()
  ws = null
  wsConnected.value = false
  wsLog.value.unshift('[断开] 手动断开')
}

function wsSend() {
  if (!ws || ws.readyState !== WebSocket.OPEN) return
  let params: Record<string, string> = {}
  try { params = JSON.parse(wsParams.value) } catch { /* ignore */ }
  const msg: Record<string, unknown> = { target: wsTarget.value, action: wsAction.value }
  if (Object.keys(params).length) msg.parameters = params
  ws.send(JSON.stringify(msg))
  wsLog.value.unshift(`[发送] ${JSON.stringify(msg)}`)
}
</script>

<template>
  <div class="app">
    <header>
      <h1>DeviceHub Demo</h1>
      <span class="tagline">接口测试工具</span>
      <select class="lang-select" :value="lang" @change="changeLang">
        <option value="en-US">English</option>
        <option value="zh-CN">中文</option>
      </select>
      <span class="hint">→ 确保后端已在 localhost:5000 运行</span>
    </header>

    <nav class="tabs">
      <button v-for="t in tabs" :key="t.key"
        :class="{ active: activeTab === t.key }"
        @click="activeTab = t.key">{{ t.label }}</button>
    </nav>

    <main>
      <!-- ========== STATUS ========== -->
      <section v-if="activeTab === 'status'">
        <div class="section-header">
          <h2>系统状态</h2>
          <code>GET /api/status</code>
          <button class="primary" @click="getStatus">请求</button>
        </div>
        <p class="desc">返回服务版本、平台、运行时长、WebSocket 连接数、驱动列表</p>
        <pre v-if="results.status" class="result">{{ results.status }}</pre>
        <pre v-else-if="errors.status" class="error-result">{{ errors.status }}</pre>
        <pre v-else class="placeholder">点击「请求」按钮查看结果</pre>
      </section>

      <!-- ========== CONFIG ========== -->
      <section v-if="activeTab === 'config'">
        <div class="section-header">
          <h2>配置管理</h2>
          <div class="btn-group">
            <button class="primary" @click="getConfig">GET /api/config</button>
            <button class="primary" @click="putConfig">PUT /api/config</button>
            <button class="danger-btn" @click="resetConfig">POST /api/config/reset</button>
          </div>
        </div>
        <p class="desc">获取或更新运行时配置。修改 Drivers.&lt;Name&gt;.Enabled 可启用/禁用驱动。红色按钮恢复出厂默认值</p>
        <div v-if="results.configGet || errors.configGet || results.configReset || errors.configReset" class="dual-panel">
          <div class="panel">
            <h3>GET 响应</h3>
            <pre v-if="results.configGet" class="result">{{ results.configGet }}</pre>
            <pre v-else class="error-result">{{ errors.configGet }}</pre>
            <div v-if="results.configReset || errors.configReset" style="margin-top:8px">
              <h3>Reset 结果</h3>
              <pre v-if="results.configReset" class="result">{{ results.configReset }}</pre>
              <pre v-else class="error-result">{{ errors.configReset }}</pre>
            </div>
          </div>
          <div class="panel">
            <h3>PUT 请求体（编辑后提交）</h3>
            <textarea v-model="configPutBody" rows="14" class="code-textarea"></textarea>
            <div v-if="results.configPut" class="panel-result">
              <h3>PUT 响应</h3>
              <pre class="result">{{ results.configPut }}</pre>
            </div>
            <pre v-else-if="errors.configPut" class="error-result">{{ errors.configPut }}</pre>
          </div>
        </div>
        <div v-else class="hint-row">
          <button class="link" @click="getConfig(); autoFillConfig()">点击 GET 后自动填充到编辑框</button>
        </div>
      </section>

      <!-- ========== LOGS ========== -->
      <section v-if="activeTab === 'logs'">
        <div class="section-header">
          <h2>日志查询</h2>
          <code>GET /api/logs?level=&amp;tail=&amp;query=</code>
          <button class="primary" @click="getLogs">请求</button>
        </div>
        <div class="filter-row">
          <label>Level <select v-model="logsLevel">
            <option>Trace</option><option>Debug</option><option>Information</option>
            <option>Warning</option><option>Error</option>
          </select></label>
          <label>Tail <input v-model.number="logsTail" type="number" min="10" max="500" /></label>
          <label>Query <input v-model="logsQuery" placeholder="搜索关键词" /></label>
        </div>
        <pre v-if="results.logs" class="result">{{ results.logs }}</pre>
        <pre v-else-if="errors.logs" class="error-result">{{ errors.logs }}</pre>
        <pre v-else class="placeholder">点击「请求」按钮查看结果</pre>
      </section>

      <!-- ========== DRIVERS ========== -->
      <section v-if="activeTab === 'drivers'">
        <div class="section-header">
          <h2>驱动管理</h2>
          <button class="primary" @click="getDrivers">GET /api/drivers</button>
        </div>
        <p class="desc">列出所有已注册驱动，可逐一启用/禁用</p>

        <div v-if="results.driversGet || errors.driversGet">
          <div v-if="driverList.length" class="driver-cards">
            <div v-for="d in driverList" :key="d.name" class="driver-card">
              <div class="driver-info">
                <strong>{{ d.name }}</strong>
                <span :class="['badge', d.enabled ? 'badge-on' : 'badge-off']">{{ d.enabled ? '已启用' : '已禁用' }}</span>
                <span class="driver-status">状态: {{ d.status }}</span>
              </div>
              <div class="btn-group">
                <button :disabled="d.enabled" @click="enableDriver(d.name)">POST enable</button>
                <button :disabled="!d.enabled" @click="disableDriver(d.name)">POST disable</button>
              </div>
              <pre v-if="results[`driversEnable_${d.name}`] || results[`driversDisable_${d.name}`]" class="mini-result">
                {{ results[`driversEnable_${d.name}`] || results[`driversDisable_${d.name}`] }}</pre>
              <pre v-else-if="errors[`driversEnable_${d.name}`] || errors[`driversDisable_${d.name}`]" class="error-result mini-result">
                {{ errors[`driversEnable_${d.name}`] || errors[`driversDisable_${d.name}`] }}</pre>
            </div>
          </div>
          <div v-else>
            <pre class="result">{{ results.driversGet }}</pre>
          </div>
        </div>
        <pre v-else class="placeholder">点击「请求」按钮查看结果</pre>
      </section>

      <!-- ========== SERVICE ========== -->
      <section v-if="activeTab === 'service'">
        <div class="section-header">
          <h2>服务控制</h2>
          <button class="primary danger-btn" @click="restartService">POST /api/service/restart</button>
        </div>
        <p class="desc">请求后服务将在 1 秒后自动重启（连接会中断）</p>
        <pre v-if="results.serviceRestart" class="result">{{ results.serviceRestart }}</pre>
        <pre v-else-if="errors.serviceRestart" class="error-result">{{ errors.serviceRestart }}</pre>
        <pre v-else class="placeholder">点击按钮重启服务</pre>
      </section>

      <!-- ========== HEALTH ========== -->
      <section v-if="activeTab === 'health'">
        <div class="section-header">
          <h2>健康检查</h2>
          <code>GET /api/health</code>
          <button class="primary" @click="getHealth">请求</button>
        </div>
        <pre v-if="results.health" class="result">{{ results.health }}</pre>
        <pre v-else-if="errors.health" class="error-result">{{ errors.health }}</pre>
        <pre v-else class="placeholder">点击「请求」按钮查看结果</pre>
      </section>

      <!-- ========== PCSC ========== -->
      <section v-if="activeTab === 'pcsc'">
        <div class="section-header">
          <h2>PCSC 读卡器</h2>
          <button class="primary" @click="listReaders">GET /api/hardware/pcsc/readers</button>
        </div>
        <pre v-if="results.pcscReaders" class="result">{{ results.pcscReaders }}</pre>
        <pre v-else-if="errors.pcscReaders" class="error-result">{{ errors.pcscReaders }}</pre>

        <hr />
        <div class="filter-row">
          <label>读卡器名称 <input v-model="pcscReaderName" style="width:300px" /></label>
        </div>
        <div class="btn-group">
          <button class="primary" @click="getReaderInfo">GET /readers/{name}</button>
          <button class="primary" @click="getAtr">GET /readers/{name}/atr</button>
        </div>
        <div v-if="results.pcscInfo || errors.pcscInfo">
          <h3>Reader Info 响应</h3>
          <pre v-if="results.pcscInfo" class="result">{{ results.pcscInfo }}</pre>
          <pre v-else class="error-result">{{ errors.pcscInfo }}</pre>
        </div>
        <div v-if="results.pcscAtr || errors.pcscAtr">
          <h3>ATR 响应</h3>
          <pre v-if="results.pcscAtr" class="result">{{ results.pcscAtr }}</pre>
          <pre v-else class="error-result">{{ errors.pcscAtr }}</pre>
        </div>

        <hr />
        <div class="filter-row">
          <label>APDU (Hex) <input v-model="pcscApdu" style="width:400px;font-family:monospace" /></label>
        </div>
        <button class="primary" @click="transmit">POST /readers/{name}/transmit</button>
        <div v-if="results.pcscTransmit || errors.pcscTransmit">
          <h3>Transmit 响应</h3>
        <pre v-if="results.pcscTransmit" class="result">{{ results.pcscTransmit }}</pre>
        <pre v-else class="error-result">{{ errors.pcscTransmit }}</pre>
        </div>
      </section>

      <!-- ========== TRANSIT CARD ========== -->
      <section v-if="activeTab === 'transitcard'">
        <div class="section-header">
          <h2>交通卡</h2>
          <code>/api/hardware/transitcard/*</code>
        </div>

        <div class="btn-group" style="margin-bottom:8px">
          <button class="primary" @click="getTransitReaders">GET readers</button>
          <button class="primary" @click="getCardInfo">GET info</button>
          <button class="primary" @click="getBalance">GET balance</button>
          <button class="primary" @click="getTransactions">GET transactions</button>
        </div>

        <div class="filter-row">
          <label>readerName <input v-model="transitReaderName" placeholder="留空自动选择" style="width:250px" /></label>
        </div>

        <div v-if="results.transitReaders || results.transitInfo || results.transitBalance || results.transitTx" class="dual-panel">
          <div class="panel">
            <pre v-if="results.transitReaders" class="result">READERS: {{ results.transitReaders }}</pre>
            <pre v-if="results.transitInfo" class="result">INFO: {{ results.transitInfo }}</pre>
            <pre v-if="results.transitBalance" class="result">BALANCE: {{ results.transitBalance }}</pre>
            <pre v-if="results.transitTx" class="result">TRANSACTIONS: {{ results.transitTx }}</pre>
          </div>
        </div>
        <pre v-if="errors.transitReaders || errors.transitInfo || errors.transitBalance || errors.transitTx" class="error-result">
          {{ errors.transitReaders || errors.transitInfo || errors.transitBalance || errors.transitTx }}</pre>

        <hr />
        <h3 style="margin-bottom:6px">充值</h3>
        <div class="filter-row">
          <label>金额(分) <input v-model.number="transitRechargeAmount" type="number" min="1" /></label>
        </div>
        <button class="primary" @click="rechargeInit">POST recharge/init</button>
        <pre v-if="results.rechargeInit" class="result">{{ results.rechargeInit }}</pre>
        <pre v-else-if="errors.rechargeInit" class="error-result">{{ errors.rechargeInit }}</pre>

        <div v-if="results.rechargeInit" style="margin-top:8px;border:1px solid #ddd;padding:8px;border-radius:4px">
          <div class="filter-row">
            <label>sessionId <input v-model="transitSessionId" style="width:350px;font-family:monospace" /></label>
          </div>
          <div class="filter-row">
            <label>macSignature <input v-model="transitMacSignature" style="width:350px;font-family:monospace" /></label>
          </div>
          <button class="primary" @click="rechargeExecute">POST recharge/execute</button>
      <pre v-if="results.rechargeExec" class="result">{{ results.rechargeExec }}</pre>
      <pre v-else-if="errors.rechargeExec" class="error-result">{{ errors.rechargeExec }}</pre>
    </div>
  </section>

  <!-- ========== PRINTER ========== -->
  <section v-if="activeTab === 'printer'">
    <div class="section-header">
      <h2>打印机</h2>
      <code>/api/hardware/printer/*</code>
    </div>

    <div class="btn-group" style="margin-bottom:8px">
      <button class="primary" @click="getPrinters">GET printers</button>
    </div>

    <div v-if="results.printerList || errors.printerList">
      <pre v-if="results.printerList" class="result">{{ results.printerList }}</pre>
      <pre v-else-if="errors.printerList" class="error-result">{{ errors.printerList }}</pre>
    </div>

    <hr />
    <h3 style="margin-bottom:6px">打印文本</h3>
    <div class="filter-row">
      <label>printerName <input v-model="printerName" placeholder="留空使用默认" style="width:250px" /></label>
    </div>
    <div class="filter-row">
      <label>text <textarea v-model="printerText" rows="3" class="code-textarea" placeholder="要打印的文本内容"></textarea></label>
    </div>
    <button class="primary" @click="printText">POST print</button>
    <pre v-if="results.printerPrint" class="result">{{ results.printerPrint }}</pre>
    <pre v-else-if="errors.printerPrint" class="error-result">{{ errors.printerPrint }}</pre>

    <hr />
    <h3 style="margin-bottom:6px">打印原始数据（Hex）</h3>
    <div class="filter-row">
      <label>printerName <input v-model="printerNameRaw" placeholder="留空使用默认" style="width:250px" /></label>
    </div>
    <div class="filter-row">
      <label>data (hex) <input v-model="printerRawData" style="width:400px;font-family:monospace" placeholder="1B405B010A48656C6C6F0A1B40" /></label>
    </div>
    <button class="primary" @click="printRaw">POST print-raw</button>
    <pre v-if="results.printerRaw" class="result">{{ results.printerRaw }}</pre>
    <pre v-else-if="errors.printerRaw" class="error-result">{{ errors.printerRaw }}</pre>
  </section>

  <!-- ========== WEBSOCKET ========== -->
      <section v-if="activeTab === 'ws'">
        <div class="section-header">
          <h2>WebSocket</h2>
          <code>/ws</code>
          <div class="btn-group">
            <button v-if="!wsConnected" class="primary" @click="wsConnect">连接</button>
            <button v-else class="danger-btn" @click="wsDisconnect">断开</button>
          </div>
          <span :class="wsConnected ? 'status-on' : 'status-off'">
            {{ wsConnected ? '已连接' : '未连接' }}
          </span>
        </div>

        <div class="filter-row">
          <label>target <input v-model="wsTarget" /></label>
          <label>action <input v-model="wsAction" /></label>
        </div>
        <div class="filter-row">
          <label>parameters (JSON) <textarea v-model="wsParams" rows="2" class="code-textarea"></textarea></label>
        </div>
        <button class="primary" :disabled="!wsConnected" @click="wsSend">发送</button>

        <h3 style="margin-top:12px">消息日志</h3>
        <div class="ws-log">
          <div v-for="(msg, i) in wsLog" :key="i" class="ws-msg">{{ msg }}</div>
        </div>
      </section>
    </main>
  </div>
</template>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f5f5f5; color: #333; }
.app { max-width: 1100px; margin: 0 auto; padding: 16px; }

/* header */
header { display: flex; align-items: baseline; gap: 6px; margin-bottom: 16px; flex-wrap: wrap; }
header h1 { font-size: 20px; }
.tagline { color: #888; font-size: 13px; }
.lang-select { padding: 2px 6px; border: 1px solid #ccc; border-radius: 3px; font-size: 12px; cursor: pointer; }
.hint { color: #999; font-size: 12px; margin-left: auto; }

/* tabs */
.tabs { display: flex; gap: 3px; flex-wrap: wrap; margin-bottom: 16px; }
.tabs button { padding: 5px 14px; border: 1px solid #ccc; background: #fff; cursor: pointer; border-radius: 4px; font-size: 12px; font-weight: 600; letter-spacing: 0.5px; }
.tabs button.active { background: #1a73e8; color: #fff; border-color: #1a73e8; }
.tabs button:hover:not(.active) { background: #e8e8e8; }

/* sections */
main section { background: #fff; border: 1px solid #ddd; border-radius: 6px; padding: 16px; margin-bottom: 16px; }
.section-header { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 8px; }
.section-header h2 { font-size: 15px; margin: 0; }
.section-header code { font-size: 11px; color: #888; background: #f0f0f0; padding: 2px 6px; border-radius: 3px; }
.desc { font-size: 12px; color: #777; margin-bottom: 10px; }

/* buttons */
.btn-group { display: flex; gap: 4px; flex-wrap: wrap; }
button { padding: 5px 12px; border: 1px solid #999; background: #fff; color: #333; cursor: pointer; border-radius: 3px; font-size: 12px; }
button:hover:not(:disabled) { background: #eee; }
button:disabled { opacity: 0.35; cursor: default; }
button.primary { background: #1a73e8; color: #fff; border-color: #1a73e8; }
button.primary:hover:not(:disabled) { opacity: 0.85; background: #1a73e8; }
button.danger-btn { background: #d32f2f; color: #fff; border-color: #d32f2f; }
button.danger-btn:hover:not(:disabled) { opacity: 0.85; background: #d32f2f; }
button.link { background: none; border: none; color: #1a73e8; text-decoration: underline; padding: 0; font-size: 12px; cursor: pointer; }

/* pre */
pre { background: #f8f8f8; border: 1px solid #eee; border-radius: 4px; padding: 8px; font-size: 11px; line-height: 1.5; overflow-x: auto; white-space: pre-wrap; word-break: break-all; margin: 6px 0; }
pre.result { border-color: #c8e6c9; background: #f1f8e9; }
pre.error-result { border-color: #ffcdd2; background: #fce4ec; color: #c62828; }
pre.placeholder { color: #bbb; border-style: dashed; }
pre.mini-result { font-size: 10px; margin: 4px 0 0; padding: 4px 6px; }

/* filter row */
.filter-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 8px; }
.filter-row label { font-size: 12px; display: flex; align-items: center; gap: 4px; }
.filter-row input, .filter-row select { padding: 3px 5px; border: 1px solid #ccc; border-radius: 3px; font-size: 12px; }
.filter-row select { background: #fff; }

/* textarea */
textarea.code-textarea { width: 100%; font-family: 'SF Mono', 'Cascadia Code', 'Consolas', monospace; font-size: 11px; padding: 6px; border: 1px solid #ccc; border-radius: 3px; resize: vertical; }

/* hr */
hr { border: none; border-top: 1px solid #eee; margin: 10px 0; }

/* dual panel */
.dual-panel { display: flex; gap: 12px; }
.panel { flex: 1; min-width: 0; }

/* driver cards */
.driver-cards { display: flex; gap: 8px; flex-wrap: wrap; }
.driver-card { border: 1px solid #ddd; border-radius: 5px; padding: 10px; flex: 1; min-width: 200px; }
.driver-info { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; flex-wrap: wrap; }
.driver-info strong { font-size: 13px; }
.driver-status { font-size: 11px; color: #888; }
.badge { font-size: 10px; padding: 1px 6px; border-radius: 3px; font-weight: 600; }
.badge-on { background: #c8e6c9; color: #2e7d32; }
.badge-off { background: #eee; color: #888; }

/* ws */
.ws-log { background: #1e1e1e; color: #d4d4d4; border-radius: 4px; padding: 8px; max-height: 350px; overflow-y: auto; font-family: 'SF Mono', 'Cascadia Code', 'Consolas', monospace; font-size: 11px; line-height: 1.5; }
.ws-msg { white-space: pre-wrap; word-break: break-all; padding: 1px 0; border-bottom: 1px solid #333; }

/* status indicator */
.status-on { color: #2e7d32; font-weight: 600; font-size: 12px; }
.status-off { color: #999; font-size: 12px; }
</style>
