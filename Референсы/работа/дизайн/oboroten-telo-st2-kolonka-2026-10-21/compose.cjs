const fs=require('fs');
const path=require('path');
const sharp=require('C:/Users/semion/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out=__dirname;
const data=n=>'data:image/png;base64,'+fs.readFileSync(path.join(out,n)).toString('base64');
async function render(name,svg){fs.writeFileSync(path.join(out,name+'.svg'),svg);await sharp(Buffer.from(svg)).png().toFile(path.join(out,name+'.png'));}
(async()=>{
 await render('source-column-st2',`<svg xmlns="http://www.w3.org/2000/svg" width="320" height="990" viewBox="800 0 320 990"><image width="1536" height="1024" href="${data('source-stupeni.png')}"/></svg>`);
 if(fs.existsSync(path.join(out,'body.png'))){
 const m=await sharp(path.join(out,'body.png')).metadata();
 const {data:raw,info}=await sharp(path.join(out,'body.png')).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const boxes=[];
 for(const [name,a,b] of [['front',0,580],['left',580,1100],['three-quarter',1100,m.width]]){
  let x0=b,y0=m.height,x1=a,y1=0;
  for(let y=0;y<m.height;y++)for(let x=a;x<b;x++){const i=(y*m.width+x)*info.channels;if(Math.max(raw[i],raw[i+1],raw[i+2])<210){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}}
  boxes.push({name,x0,y0,x1,y1,height:y1-y0+1});
 }
 fs.writeFileSync(path.join(out,'view-bounds.json'),JSON.stringify({threshold:'max RGB < 210',width:m.width,height:m.height,views:boxes},null,2));
 let aligned='<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="900"><rect width="1800" height="900" fill="white"/>';
 boxes.forEach((b,i)=>{const k=800/(b.y1-b.y0),x=300+i*600-(b.x0+b.x1)/2*k,y=850-b.y1*k,cx=x+(b.x0-4)*k,cy=y+(b.y0-4)*k,cw=(b.x1-b.x0+8)*k,ch=(b.y1-b.y0+8)*k;aligned+=`<defs><clipPath id="v${i}"><rect x="${cx}" y="${cy}" width="${cw}" height="${ch}"/></clipPath></defs><image x="${x}" y="${y}" width="${m.width*k}" height="${m.height*k}" clip-path="url(#v${i})" href="${data('body.png')}"/>`;});
 await render('body-aligned',aligned+'</svg>');
 await render('comparison',`<svg xmlns="http://www.w3.org/2000/svg" width="2300" height="1500"><rect width="2300" height="1500" fill="white"/><g font-family="Arial" font-size="25" fill="#202530"><text x="35" y="40">Исходная колонка ст.2</text><text x="450" y="40">Голое тело: анфас / левый профиль / 3/4</text></g><image x="35" y="65" width="320" height="990" href="${data('source-column-st2.png')}"/><image x="420" y="80" width="1850" height="${Math.round(1850*m.height/m.width)}" href="${data('body.png')}"/></svg>`);
 }
})().catch(e=>{console.error(e);process.exit(1)});
