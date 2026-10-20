const sharp = require('C:/Users/semion/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const path = require('path');
const name = process.argv[2] || 'preview';
sharp(path.join(__dirname, name+'.svg')).png().toFile(path.join(__dirname, name+'.png')).then(()=>console.log(name+'.png')).catch(e=>{console.error(e);process.exit(1)});
