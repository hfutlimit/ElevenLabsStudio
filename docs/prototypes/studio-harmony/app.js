/* Standalone design prototype. All agent data and publishing are simulated. */
const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
const icons = {
  agents: '<rect x="5" y="7" width="14" height="12" rx="4"/><path d="M12 3v4M9 12h.01M15 12h.01M9 16h6M2 11v4M22 11v4"/>',
  audio: '<path d="M4 10v4M8 6v12M12 3v18M16 7v10M20 10v4"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  download: '<path d="M12 3v12m-4-4 4 4 4-4M5 16v4h14v-4"/>',
  upload: '<path d="M12 16V4m-4 4 4-4 4 4M5 16v4h14v-4"/>',
  layers: '<path d="m12 3 9 5-9 5-9-5 9-5Zm-9 9 9 5 9-5M3 16l9 5 9-5"/>',
  settings: '<path d="m10 3-.6 3-2 .9-2.7-1-2 3.4L5 11v2l-2.3 1.7 2 3.4 2.7-1 2 .9.6 3h4l.6-3 2-.9 2.7 1 2-3.4L19 13v-2l2.3-1.7-2-3.4-2.7 1-2-.9L14 3Z"/><circle cx="12" cy="12" r="3"/>',
  'chevron-right': '<path d="m9 6 6 6-6 6"/>',
  moon: '<path d="M20.5 14A9 9 0 0 1 10 3.5 9 9 0 1 0 20.5 14Z"/>',
  help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 1 1 4 2c-1.5 1-1.5 1-1.5 3M12 17h.01"/>',
  copy: '<rect x="8" y="8" width="12" height="13" rx="2"/><path d="M16 8V3H3v13h5"/>',
  play: '<path d="m8 4 12 8-12 8Z"/>',
  align: '<path d="M4 5h16M4 10h11M4 15h16M4 20h11"/>',
  message: '<path d="M21 14a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4Z"/><path d="M7 8h10M7 12h7"/>',
  sparkles: '<path d="m12 3 2.4 6.6L21 12l-6.6 2.4L12 21l-2.4-6.6L3 12l6.6-2.4ZM20 2v4M18 4h4"/>',
  'arrow-right': '<path d="M4 12h16m-6-6 6 6-6 6"/>',
  'arrow-up': '<path d="M12 20V4m-6 6 6-6 6 6"/>',
  'arrow-up-right': '<path d="M6 18 18 6M6 6h12v12"/>',
  shield: '<path d="M12 3 4 6v6c0 5 8 9 8 9s8-4 8-9V6Z"/><path d="m8 12 3 3 5-6"/>',
  check: '<path d="m5 12 4 4L19 6"/>',
  branches: '<circle cx="6" cy="5" r="2"/><circle cx="6" cy="19" r="2"/><circle cx="18" cy="5" r="2"/><path d="M6 7v10M18 7v2a4 4 0 0 1-4 4H6"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7h.01"/>',
  sliders: '<path d="M4 6h4m4 0h8M4 12h10m4 0h2M4 18h2m4 0h10"/><circle cx="10" cy="6" r="2"/><circle cx="16" cy="12" r="2"/><circle cx="8" cy="18" r="2"/>',
  headphones: '<path d="M3 14v-3a9 9 0 0 1 18 0v3"/><rect x="3" y="12" width="4" height="8" rx="2"/><rect x="17" y="12" width="4" height="8" rx="2"/>',
  close: '<path d="m6 6 12 12M6 18 18 6"/>',
  trash: '<path d="M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7"/>',
  globe: '<circle cx="12" cy="12" r="9"/><ellipse cx="12" cy="12" rx="4" ry="9"/><path d="M3 12h18"/>',
};
function drawIcons(root = document) {
  $$('[data-icon]', root).forEach(el => el.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[el.dataset.icon] || icons.audio}</svg>`);
}
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
icons.refresh = '<path d="M20 7v5h-5M4 17v-5h5"/><path d="M6 6a8 8 0 0 1 14 6M18 18A8 8 0 0 1 4 12"/>';
icons.panel = '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16m7-12-3 4 3 4"/>';
const initialPrompt = `# Role
You are a helpful customer support assistant for Acme.
Be calm, friendly, and concise in every response.

# Goals
Understand the customer's question before offering a solution.
Use {{customer_name}} when addressing the customer.
Refer to {{company_name}} when discussing company policies.

# Conversation guidelines
Ask one question at a time.
Use short, natural sentences that are easy to understand.
Confirm important details before moving to the next step.

# Boundaries
Do not guess account information or invent policy details.
If you cannot resolve an issue, offer to transfer the customer
to a human support specialist.`;
const agents = [
  {id:'agent_care_8f2a',name:'Customer Care'},
  {id:'agent_sales_4d1b',name:'Sales Assistant'},
  {id:'agent_booking_9c3e',name:'Booking Assistant'},
  {id:'agent_concierge_2a7f',name:'Travel Concierge'},
];
const states = new Map();
function newState(name) {
  const base = {prompt:initialPrompt.replace('customer support assistant', name.toLowerCase() + ' assistant'),first:'Hi, thanks for calling Acme. How can I help you today?',variables:[{name:'customer_name',value:'Alex',type:'String'},{name:'company_name',value:'Acme',type:'String'}],nodes:[{id:'start',name:'Start',type:'start'},{id:'support',name:'Customer support',type:'agent'},{id:'end',name:'End conversation',type:'end'}]};
  return {base:structuredClone(base),draft:structuredClone(base),saved:null};
}
agents.forEach(a => states.set(a.id,newState(a.name)));
let selectedId = agents[0].id;
let activeTab = 'prompt';
let selectedNodeId = null;
let liveConnected = false;
let timer = null;
let startedAt = 0;
let liveVariables = [{key:'customer_name',value:'Alex'},{key:'account_found',value:'true'}];
const current = () => states.get(selectedId);
const agent = () => agents.find(a => a.id === selectedId);
const same = (a,b) => JSON.stringify(a) === JSON.stringify(b);
const dirty = () => !same(current().draft,current().base);
const unsaved = () => dirty() && !same(current().draft,current().saved);
let toastTimer;
function toast(text) { clearTimeout(toastTimer); $('#toast').textContent=text; $('#toast').hidden=false; toastTimer=setTimeout(() => $('#toast').hidden=true,3600); }
function renderAgents() {
  $('#agent-count').textContent=agents.length;
  $('#agent-list').innerHTML=agents.map(a=>`<button class="agent-item ${a.id===selectedId?'selected':''}" data-agent="${esc(a.id)}" aria-current="${a.id===selectedId?'page':'false'}"><span class="agent-icon"><i data-icon="agents"></i></span><span><strong>${esc(a.name)}</strong><small>${esc(a.id)}</small></span>${a.id===selectedId?'<span class="selection-dot"></span>':''}</button>`).join('');
  drawIcons($('#agent-list'));
}
function suggestionMarkup(field,severity,text) {
  return `<div class="suggestion-label">LOCAL SUGGESTIONS</div><div class="suggestion-row"><span class="suggestion-icon"><i data-icon="info"></i></span><div><div class="suggestion-title">${field}<span class="severity">${severity}</span></div><p>${text}</p></div></div>`;
}
function suggestions() {
  const prompt=current().draft.prompt;
  let text='', severity='Info';
  if(!prompt.trim()){text="An empty prompt leaves the Agent's behavior undefined.";severity='Warning';}
  else if(prompt.length>8000){text='Prompts over 8,000 characters may be truncated or rejected by ElevenLabs.';severity='Warning';}
  else if(prompt===current().base.prompt){text='The prompt is unchanged; there is nothing to push.';}
  $('#prompt-suggestions').innerHTML=text?suggestionMarkup('Prompt',severity,text):'';
  $('#first-suggestions').innerHTML=current().draft.first.trim()?'':suggestionMarkup('FirstMessage','Suggestion','An empty first message means the Agent will not open the conversation proactively.');
  drawIcons($('#prompt-suggestions'));drawIcons($('#first-suggestions'));
}
function update() {
  $('#sync-label').textContent=dirty()?'Unsaved changes':'In sync with server';
  $('#sync-dot').classList.toggle('dirty',dirty());$('#save-dot').classList.toggle('dirty',dirty());
  $('#save-text').textContent=dirty()?(unsaved()?'Draft edits — not on the server yet':'Draft saved — not on the server yet'):'Matches the server snapshot';
  $('#save-draft').disabled=!unsaved();$('#publish').disabled=!dirty();
  $('#prompt-count').textContent=`${current().draft.prompt.length.toLocaleString()} chars`;
  $('#first-count').textContent=`${current().draft.first.length.toLocaleString()} chars`;
  suggestions();
}
function selectAgent(id) {
  endSession();selectedId=id;selectedNodeId=null;
  $('#agent-name').textContent=agent().name;$('#agent-id').textContent=agent().id;$('#live-agent-name').textContent=`${agent().name} · ${agent().id}`;
  $('#prompt-editor').value=current().draft.prompt;$('#first-editor').value=current().draft.first;
  $('#live-messages').innerHTML='<div class="empty-state"><i data-icon="message"></i><h3>No transcript yet</h3><p>Start a conversation to see live messages here.</p></div>';
  $('#live-id').textContent='—';$('#elapsed').textContent='00:00';
  renderAgents();renderVariables();renderWorkflow();update();drawIcons($('#live-messages'));
}
function setTab(tab,focus=false) {
  activeTab=tab;
  $$('.tab').forEach(el=>{const selected=el.dataset.tab===tab;el.classList.toggle('active',selected);el.setAttribute('aria-selected',selected);el.tabIndex=selected?0:-1;});
  $$('.tab-panel').forEach(el=>el.hidden=el.id!==`panel-${tab}`);
  $('.content-scroll').scrollTop=0;if(focus)$(`#tab-${tab}`).focus();
}
let onConfirm=null;
function openDialog(title,content,label,action) {
  $('#dialog-title').textContent=title;$('#dialog-content').innerHTML=content;$('#confirm-dialog').textContent=label;onConfirm=action;
  $('#dialog').showModal();const input=$('#dialog-content input');if(input)input.focus();
}
$('#dialog-form').onsubmit=event=>{event.preventDefault();if(onConfirm?.()!==false)$('#dialog').close();};
$('#cancel-dialog').onclick=$('#close-dialog').onclick=()=>$('#dialog').close();
function guardSwitch(id) {
  if(id===selectedId)return;
  if(!unsaved())return selectAgent(id);
  openDialog('Unsaved changes',`<p>Save edits to <strong>${esc(agent().name)}</strong> as a draft before switching?</p><button class="button secondary" type="button" id="discard-and-switch">Discard changes</button>`,'Save draft & continue',()=>{current().saved=structuredClone(current().draft);selectAgent(id);});
  $('#discard-and-switch').onclick=()=>{current().draft=structuredClone(current().base);current().saved=null;$('#dialog').close();selectAgent(id);};
}
$('#agent-list').onclick=e=>{const target=e.target.closest('[data-agent]');if(target)guardSwitch(target.dataset.agent);};
$$('.tab').forEach(el=>el.onclick=()=>setTab(el.dataset.tab));
$('.tabs').onkeydown=e=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;e.preventDefault();const tabs=$$('.tab');let index=tabs.findIndex(t=>t.dataset.tab===activeTab);index=e.key==='Home'?0:e.key==='End'?tabs.length-1:(index+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length;setTab(tabs[index].dataset.tab,true);};
$('#prompt-editor').oninput=e=>{current().draft.prompt=e.target.value;update();};
$('#first-editor').oninput=e=>{current().draft.first=e.target.value;update();};
$('#dry-run').onclick=()=>{suggestions();toast('Local suggestions refreshed. No API request was made.');};
$('#save-draft').onclick=()=>{current().saved=structuredClone(current().draft);update();toast('Draft saved for this prototype session.');};
$('#reload-agent').onclick=()=>openDialog('Reload agent', '<p>Reload the server snapshot? Local edits and saved drafts for this Agent will be replaced.</p><p class="dialog-note">Prototype: restores the sample snapshot only.</p>', 'Reload',()=>{current().draft=structuredClone(current().base);current().saved=null;selectAgent(selectedId);toast('Sample snapshot reloaded.');});
$('#publish').onclick=()=>{
  const labels={prompt:'System prompt',first:'First message',variables:'Variables',nodes:'Workflow'};
  const changes=Object.keys(labels).filter(k=>!same(current().draft[k],current().base[k])).map(k=>labels[k]);
  openDialog('Push changes',`<p>Update <strong>${esc(agent().name)}</strong> with the following edits?</p><p class="dialog-note">${changes.join(' · ')}</p><p>Prototype only. No remote data will be changed.</p>`,'Push to server',()=>{current().base=structuredClone(current().draft);current().saved=null;update();$('#raw-json').textContent=JSON.stringify({nodes:current().base.nodes},null,2);toast('Push state updated locally. No API request was made.');});
};
$('#collapse-sidebar').onclick=()=>{const collapsed=$('.app').classList.toggle('collapsed');$('#collapse-sidebar').setAttribute('aria-label',`${collapsed?'Expand':'Collapse'} agents sidebar`);$('#collapse-sidebar').title=`${collapsed?'Expand':'Collapse'} agents sidebar`;};
$('#import-agent').onclick=()=>openDialog('Import an agent into the app','<p>Enter an ElevenLabs Agent ID to load its full configuration.</p><label>Agent ID<input id="import-id" placeholder="agent_…" pattern="agent_[A-Za-z0-9_-]+" required maxlength="100"></label><p class="hint">Layout prototype: sample configuration only.</p>','Import Agent',()=>{const id=$('#import-id').value.trim();if(agents.some(a=>a.id===id)){toast('This Agent is already in the list.');return false;}const a={id,name:'Imported Agent'};agents.push(a);states.set(id,newState(a.name));renderAgents();toast('Sample Agent added to the list.');});
function renderVariables(){
  $('#variable-rows').innerHTML=current().draft.variables.map((v,i)=>`<tr>${['name','value','type'].map(k=>`<td><input data-variable="${i}" data-field="${k}" aria-label="Variable ${i+1} ${k}" value="${esc(v[k])}"></td>`).join('')}<td><button class="icon-button" data-remove-variable="${i}" aria-label="Delete variable ${i+1}" title="Delete variable"><i data-icon="trash"></i></button></td></tr>`).join('')||'<tr><td colspan="4" class="muted">No variables.</td></tr>';drawIcons($('#variable-rows'));
}
$('#add-variable').onclick=()=>{current().draft.variables.push({name:'',value:'',type:'String'});renderVariables();update();$('#variable-rows tr:last-child input').focus();};
$('#variable-rows').oninput=e=>{if(e.target.matches('[data-variable]')){current().draft.variables[Number(e.target.dataset.variable)][e.target.dataset.field]=e.target.value;update();}};
$('#variable-rows').onclick=e=>{const button=e.target.closest('[data-remove-variable]');if(button){current().draft.variables.splice(Number(button.dataset.removeVariable),1);renderVariables();update();}};
function renderWorkflow(){
  const nodes=current().draft.nodes;
  $('#workflow-nodes').innerHTML=nodes.map((n,i)=>`${i?`<button class="edge-button" data-edge="${i-1}">Continue</button>`:''}<div class="flow-node ${n.id===selectedNodeId?'selected':''}" data-node="${esc(n.id)}" tabindex="0" role="button" aria-label="Inspect ${esc(n.name)}"><i data-icon="${n.type==='start'?'play':n.type==='end'?'check':'agents'}"></i><div><strong>${esc(n.name)}</strong><small>${esc(n.id)}</small></div><button class="icon-button" data-remove-node="${esc(n.id)}" aria-label="Remove ${esc(n.name)}"><i data-icon="close"></i></button></div>`).join('');
  $('#raw-json').textContent=JSON.stringify({nodes:current().base.nodes},null,2);$('#node-inspector').hidden=true;drawIcons($('#workflow-nodes'));
}
function inspectNode(id){
  selectedNodeId=id;const node=current().draft.nodes.find(n=>n.id===id);if(!node)return;
  $$('.flow-node').forEach(el=>el.classList.toggle('selected',el.dataset.node===id));
  $('#node-inspector').hidden=false;$('#inspector-title').textContent='Node';
  const index=current().draft.nodes.indexOf(node);const next=current().draft.nodes[index+1];
  $('#node-inspector-content').innerHTML=`<p class="mono">${esc(node.id)}</p><label>Name<input id="node-name" value="${esc(node.name)}"></label><label>Connections</label><p>${next?`→ ${esc(next.name)}`:'No outgoing connections.'}</p>`;
  $('#node-name').oninput=e=>{node.name=e.target.value;$$('.flow-node').find(el=>el.dataset.node===id).querySelector('strong').textContent=node.name;update();};
}
$('#workflow-nodes').onclick=e=>{
  const remove=e.target.closest('[data-remove-node]');if(remove){current().draft.nodes=current().draft.nodes.filter(n=>n.id!==remove.dataset.removeNode);renderWorkflow();update();return;}
  const edge=e.target.closest('[data-edge]');if(edge){const index=Number(edge.dataset.edge);const nodes=current().draft.nodes;$('#node-inspector').hidden=false;$('#inspector-title').textContent='Connection';$('#node-inspector-content').innerHTML=`<p>${esc(nodes[index].name)} → ${esc(nodes[index+1].name)}</p><label>Condition</label><p>Continue</p><p class="hint">Read-only</p>`;return;}
  const node=e.target.closest('[data-node]');if(node)inspectNode(node.dataset.node);
};
$('#workflow-nodes').onkeydown=e=>{if((e.key==='Enter'||e.key===' ')&&e.target.matches('[data-node]')){e.preventDefault();inspectNode(e.target.dataset.node);}};
$('#close-inspector').onclick=()=>{selectedNodeId=null;$('#node-inspector').hidden=true;$$('.flow-node').forEach(el=>el.classList.remove('selected'));};
let nodeSequence=1;
$('#add-node').onclick=()=>{const n={id:`node_${nodeSequence++}`,name:'New node',type:'agent'};current().draft.nodes.push(n);renderWorkflow();inspectNode(n.id);update();};
function workflowTab(json){$('#workflow-canvas').hidden=json;$('#workflow-json').hidden=!json;$('#workflow-json-tab').setAttribute('aria-pressed',json);$('#workflow-canvas-tab').setAttribute('aria-pressed',!json);}
$('#workflow-canvas-tab').onclick=()=>workflowTab(false);$('#workflow-json-tab').onclick=()=>workflowTab(true);
const conversations=[{id:'conv_84e2a91',time:'09-24 14:32',status:'done'},{id:'conv_7b91c02',time:'09-24 11:08',status:'done'},{id:'conv_6a20f83',time:'09-23 16:45',status:'done'}];
function selectConversation(index){
  $('#conversation-list').innerHTML=conversations.map((c,i)=>`<button class="conversation-item ${i===index?'selected':''}" data-conversation="${i}"><div><span class="status-badge">${c.status}</span><time>${c.time}</time></div><small>${c.id}</small></button>`).join('');
  $('#conversation-id').textContent=conversations[index].id;
  const question=['Could you help me track my order?','I need to update my account details.','Can I check the status of my refund?'][index];
  $('#transcript').innerHTML=[['agent','00:00','Hi, thanks for calling Acme. How can I help you today?'],['user','00:05',question],['agent','00:12','Of course. Could you share the reference number with me?']].map(([who,time,text])=>`<div class="transcript-turn"><div>${who}<time>${time}</time></div><p>${text}</p></div>`).join('');
}
$('#conversation-list').onclick=e=>{const row=e.target.closest('[data-conversation]');if(row)selectConversation(Number(row.dataset.conversation));};
$('#reload-conversations').onclick=()=>{selectConversation(0);toast('Sample conversations refreshed.');};
function renderLiveVariables(){
  $('#live-variables').innerHTML=liveVariables.map((v,i)=>`<div class="live-variable-row"><input data-live-variable="${i}" data-field="key" aria-label="Session variable ${i+1} key" value="${esc(v.key)}" ${liveConnected?'disabled':''}><input data-live-variable="${i}" data-field="value" aria-label="Session variable ${i+1} value" value="${esc(v.value)}" ${liveConnected?'disabled':''}><button class="icon-button" data-remove-live="${i}" aria-label="Remove session variable ${i+1}" ${liveConnected?'disabled':''}><i data-icon="close"></i></button></div>`).join('');drawIcons($('#live-variables'));
}
$('#live-variables').oninput=e=>{if(e.target.matches('[data-live-variable]'))liveVariables[Number(e.target.dataset.liveVariable)][e.target.dataset.field]=e.target.value;};
$('#live-variables').onclick=e=>{const button=e.target.closest('[data-remove-live]');if(button){liveVariables.splice(Number(button.dataset.removeLive),1);renderLiveVariables();}};
$('#add-live-variable').onclick=()=>{liveVariables.push({key:'',value:''});renderLiveVariables();};
$('#scenario').onchange=()=>{liveVariables=$('#scenario').value==='found'?[{key:'customer_name',value:'Alex'},{key:'account_found',value:'true'}]:[{key:'customer_name',value:''},{key:'account_found',value:'false'}];renderLiveVariables();};
function sessionState(){
  $('#start-live').disabled=liveConnected;$('#end-live').disabled=!liveConnected;$('#send-message').disabled=!liveConnected;$('#message-input').disabled=!liveConnected;$('#mute').disabled=!liveConnected;$('#live-status').textContent=liveConnected?'Connected · demo':'Disconnected';
  $$('.session-panel input, .session-panel select').forEach(el=>el.disabled=liveConnected);$('#add-live-variable').disabled=liveConnected;renderLiveVariables();
}
function endSession(){liveConnected=false;clearInterval(timer);timer=null;sessionState();$('#mute').textContent='Mute microphone';}
$('#start-live').onclick=()=>{liveConnected=true;startedAt=Date.now();sessionState();$('#live-id').textContent='conv_layout_demo';$('#elapsed').textContent='00:00';timer=setInterval(()=>{const seconds=Math.floor((Date.now()-startedAt)/1000);$('#elapsed').textContent=`${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;},1000);toast('Layout demo only. No microphone or audio connection.');};
$('#end-live').onclick=endSession;
$('#mute').onclick=()=>$('#mute').textContent=$('#mute').textContent==='Mute microphone'?'Unmute microphone':'Mute microphone';
$('#message-form').onsubmit=e=>{e.preventDefault();const text=$('#message-input').value.trim();if(!liveConnected||!text)return;const empty=$('.empty-state',$('#live-messages'));if(empty)empty.remove();const turn=document.createElement('div');turn.className='transcript-turn';turn.innerHTML=`<div>user<time>${$('#elapsed').textContent}</time></div><p>${esc(text)}</p>`;$('#live-messages').append(turn);$('#message-input').value='';$('#live-messages').scrollTop=$('#live-messages').scrollHeight;};
$('#settings').onclick=()=>openDialog('Settings','<p>Connect to ElevenLabs and configure how the app runs.</p><label>ElevenLabs API Key<input type="password" placeholder="Example key only" autocomplete="off"></label><p class="hint">You can also use the ELEVENLABS_API_KEY environment variable.</p><label>Data source</label><label><input type="checkbox" checked>Use offline mock data (no API key required)</label><p class="hint">Prototype only. Settings are not stored or sent.</p>','Save',()=>toast('Settings layout reviewed. No credentials stored.'));
window.addEventListener('beforeunload',e=>{if([...states.values()].some(s=>!same(s.draft,s.base)&&!same(s.draft,s.saved))){e.preventDefault();e.returnValue='';}});
drawIcons();selectAgent(selectedId);setTab('prompt');selectConversation(0);renderLiveVariables();
