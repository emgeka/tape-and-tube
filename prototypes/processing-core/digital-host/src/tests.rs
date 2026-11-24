// SPDX-License-Identifier: GPL-3.0-only
use super::*;
use std::ffi::CStr;

fn error(value: *mut c_char) -> Option<String> {
    if value.is_null() { return None; }
    let text = unsafe { CStr::from_ptr(value) }.to_string_lossy().into_owned();
    unsafe { tt_digital_string_free(value); }
    Some(text)
}
fn ok(value: *mut c_char) { assert_eq!(error(value),None); }
fn create(settings: Value) -> u64 {
    let text = settings.to_string();
    let mut handle = 0;
    ok(unsafe { tt_digital_create(text.as_ptr(),text.len(),&mut handle) });
    assert_ne!(handle,0);
    handle
}
struct OwnedSession(u64);
impl OwnedSession {
    fn new(settings: Value) -> Self { Self(create(settings)) }
}
impl Drop for OwnedSession {
    fn drop(&mut self) { ok(tt_digital_destroy(self.0)); }
}

fn context(index: i64) -> FrameContext {
    FrameContext {struct_size:std::mem::size_of::<FrameContext>() as u32,format:RGBA8,width:32,height:24,stride:32*4+8,capture_width:32,capture_height:24,frame_index:index,fps_num:24,fps_den:1,flags:0,reserved:0}
}
struct Fixture {
    frames: Vec<Vec<u8>>,
    requested: Vec<i64>,
    fail: bool,
    twice: bool,
    bad_format: bool,
}
impl Fixture {
    fn new() -> Self {
        let frames = (0..32).map(|n| {
            let c = context(n);
            let mut data = vec![0xA5;c.stride*c.height as usize];
            for y in 0..c.height as usize {
                for x in 0..c.width as usize {
                    let i=y*c.stride+x*4;
                    // Real changing content, with a scene cut and a moving block.
                    data[i] = ((x*11+y*3+n as usize*7)%256) as u8;
                    data[i+1] = ((y*17+n as usize*13)%256) as u8;
                    data[i+2] = if n < 12 {((x^y)*9) as u8} else {220};
                    if x >= n as usize % 20 && x < n as usize % 20 + 6 && y < 12 {data[i..i+3].copy_from_slice(&[240,15,20]);}
                    data[i+3]=77;
                }
            }
            data
        }).collect();
        Self {frames,requested:Vec::new(),fail:false,twice:false,bad_format:false}
    }
}
unsafe extern "C" fn provide(user: *mut c_void,index:i64,sink:FrameSink,sink_user:*mut c_void)->i32 {
    if user.is_null() {return 0;}
    let fixture=unsafe {&mut *(user as *mut Fixture)};
    fixture.requested.push(index);
    if fixture.fail || index<0 {return 0;}
    let Some(data)=fixture.frames.get(index as usize) else {return 0;};
    let c=context(index);
    let view=FrameView{struct_size:std::mem::size_of::<FrameView>() as u32,format:if fixture.bad_format {99}else{RGBA8},width:c.width,height:c.height,stride:c.stride,pixels:data.as_ptr(),length:data.len()};
    let status=unsafe {sink(sink_user,&view)};
    if fixture.twice {unsafe {sink(sink_user,&view);}}
    status
}
fn process(handle:u64,index:i64,fixture:&mut Fixture)->Vec<u8> {
    let c=context(index);
    let mut pixels=fixture.frames[index as usize].clone();
    ok(unsafe {tt_digital_process(handle,&c,pixels.as_mut_ptr(),pixels.len(),Some(provide),(fixture as *mut Fixture).cast())});
    pixels
}
fn settings(codec:u32,predictive:bool,loss:f64,mosh:f64)->Value {
    json!({"codec":codec,"frame_rate":12,"fields":0,"sensor_noise":0.3,"noise_reduction":0.0,"quality":0.55,"keyframe_interval":if predictive {0.5}else{0.0},"data_loss":loss,"datamosh":mosh})
}
fn temporal_equivalence(configuration:Value) {
    let straight=OwnedSession::new(configuration.clone());
    let mut source=Fixture::new();
    let reference:Vec<_>=(0..32).map(|i|process(straight.0,i,&mut source)).collect();
    assert_ne!(reference[14],source.frames[14],"test actually exercises codec processing");
    let seeking=OwnedSession::new(configuration);
    let mut seek_source=Fixture::new();
    // Start with a cold forward jump, backwards, cross a keyframe/scene cut,
    // return to start (loop), then emulate dropped preview presentation frames.
    for index in [23,7,18,0,1,6,12,24,31,3,23] {
        assert_eq!(process(seeking.0,index,&mut seek_source),reference[index as usize],"source frame {index}");
    }
    assert!(!seek_source.requested.is_empty(),"jumps fetched real history");
    ok(tt_digital_reset(seeking.0));
    assert_eq!(process(seeking.0,25,&mut seek_source),reference[25]);
    assert_eq!(process(straight.0,25,&mut source),reference[25],"independent session not affected by reset");
}

#[test] fn dct_intra_sequential_seek_loop_dropframes() {temporal_equivalence(settings(0,false,0.0,0.0));}
#[test] fn dct_intra_data_loss_sequential_seek_loop_dropframes() {temporal_equivalence(settings(0,false,0.6,0.0));}
#[test] fn dct_predictive_data_loss_sequential_seek_loop_dropframes() {temporal_equivalence(settings(0,true,0.6,0.0));}
#[test] fn dct_datamosh_sequential_seek_loop_dropframes() {temporal_equivalence(settings(0,true,0.15,1.0));}
#[test] fn video1_predictive_data_loss_sequential_seek_loop_dropframes() {temporal_equivalence(settings(1,true,0.35,0.0));}

#[test]
fn instances_do_not_share_codec_state() {
    let a=OwnedSession::new(settings(0,true,0.4,1.0));
    let b=OwnedSession::new(settings(1,true,0.1,0.0));
    let fresh_a=OwnedSession::new(settings(0,true,0.4,1.0));
    let fresh_b=OwnedSession::new(settings(1,true,0.1,0.0));
    let mut fa=Fixture::new();let mut fb=Fixture::new();
    let mut ea=Fixture::new();let mut eb=Fixture::new();
    for i in 0..32 {
        let actual_a=process(a.0,i,&mut fa);
        let actual_b=process(b.0,31-i,&mut fb);
        assert_eq!(actual_a,process(fresh_a.0,i,&mut ea));
        assert_eq!(actual_b,process(fresh_b.0,31-i,&mut eb));
    }
}

#[test]
fn rgba_color_alpha_and_stride_padding() {
    let host=OwnedSession::new(json!({"sensor_noise":0,"noise_reduction":0,"chroma":0,"fields":0,"frame_rate":24,"quality":1}));
    let c=context(0);
    let mut pixels=vec![0xC9;c.stride*c.height as usize];
    for row in pixels.chunks_mut(c.stride) {for p in row[..c.width as usize*4].chunks_mut(4) {p.copy_from_slice(&[230,20,10,18]);}}
    ok(unsafe {tt_digital_process(host.0,&c,pixels.as_mut_ptr(),pixels.len(),None,std::ptr::null_mut())});
    for row in pixels.chunks(c.stride) {
        assert!(row[c.width as usize*4..].iter().all(|v|*v==0xC9));
        for p in row[..c.width as usize*4].chunks(4) {assert!(p[0]>210 && p[1]<40 && p[2]<30);assert_eq!(p[3],255);}
    }
}

#[test]
fn history_errors_do_not_commit_output_and_session_recovers() {
    for mode in 0..4 {
        let host=OwnedSession::new(settings(0,true,0.2,1.0));
        let mut source=Fixture::new();
        source.fail=mode==1;source.twice=mode==2;source.bad_format=mode==3;
        let c=context(21);let mut pixels=source.frames[21].clone();let before=pixels.clone();
        let provider=if mode==0{None}else{Some(provide as FrameProvider)};
        assert!(error(unsafe {tt_digital_process(host.0,&c,pixels.as_mut_ptr(),pixels.len(),provider,(&mut source as *mut Fixture).cast())}).is_some());
        assert_eq!(pixels,before);
        source.fail=false;source.twice=false;source.bad_format=false;
        let fresh=OwnedSession::new(settings(0,true,0.2,1.0));
        assert_eq!(process(host.0,21,&mut source),process(fresh.0,21,&mut Fixture::new()));
    }
}

#[test]
fn invalid_inputs_and_stale_handles_return_errors() {
    let host=OwnedSession::new(json!({}));let mut pixels=Fixture::new().frames[0].clone();
    for change in 0..9 {
        let mut c=context(0);
        match change {0=>c.format=3,1=>c.stride=1,2=>c.width=0,3=>c.fps_den=0,4=>c.frame_index=-1,5=>c.capture_width=0,6=>c.flags=2,7=>c.struct_size=0,_=>c.reserved=1}
        let before=pixels.clone();
        assert!(error(unsafe {tt_digital_process(host.0,&c,pixels.as_mut_ptr(),pixels.len(),None,std::ptr::null_mut())}).is_some());
        assert_eq!(pixels,before);
    }
    assert!(error(unsafe {tt_digital_process(host.0,&context(0),pixels.as_mut_ptr(),1,None,std::ptr::null_mut())}).is_some());
    let stale=create(json!({}));ok(tt_digital_destroy(stale));
    assert!(error(tt_digital_reset(stale)).is_some());assert!(error(tt_digital_destroy(stale)).is_some());
    assert!(error(unsafe {tt_digital_process(stale,&context(0),pixels.as_mut_ptr(),pixels.len(),None,std::ptr::null_mut())}).is_some());
}

#[test]
fn settings_are_strict_and_metadata_is_truthful() {
    for bad in ["[]","{\"codec\":9}","{\"quality\":2}","{\"seed\":123}","{\"fields\":0.5}","{\"quality\":null}","{\"quality\":1e999}","{\"frame_rate\":1.5}"] {
        let mut handle=123;
        assert!(error(unsafe {tt_digital_create(bad.as_ptr(),bad.len(),&mut handle)}).is_some());assert_eq!(handle,0);
    }
    let m=tt_digital_metadata_json();assert!(!m.is_null());
    let metadata:Value=serde_json::from_slice(unsafe {CStr::from_ptr(m)}.to_bytes()).unwrap();unsafe {tt_digital_string_free(m);}
    assert_eq!(metadata["hostApi"],API_VERSION);assert_eq!(metadata["settingsHistory"],false);
    assert!(metadata["seed"].as_str().unwrap().contains("no independent seed"));
    assert_eq!(metadata["parameters"].as_array().unwrap().len(),32);
}

#[test]
fn still_mode_is_explicit_and_reproducible() {
    let host=OwnedSession::new(settings(0,true,0.3,1.0));let mut c=context(23);c.flags=STILL;
    let original=Fixture::new().frames[0].clone();let mut first=original.clone();
    ok(unsafe {tt_digital_process(host.0,&c,first.as_mut_ptr(),first.len(),None,std::ptr::null_mut())});
    ok(tt_digital_reset(host.0));let mut second=original;
    ok(unsafe {tt_digital_process(host.0,&c,second.as_mut_ptr(),second.len(),None,std::ptr::null_mut())});
    assert_eq!(first,second);
}

#[test]
fn cold_still_datamosh_seek_obeys_budget_without_calling_external_provider() {
    let configuration=json!({"frame_rate":24,"fields":0,"sensor_noise":0,"noise_reduction":0,"keyframe_interval":0.1,"datamosh":1});
    let host=OwnedSession::new(configuration.clone());
    let mut c=context(MAX_REPLAY_REQUESTS as i64+4);
    c.flags=STILL;c.width=16;c.height=16;c.capture_width=16;c.capture_height=16;c.stride=16*4+3;
    let original=vec![82u8;c.stride*c.height as usize];
    let mut pixels=original.clone();
    // This provider would fail and record a call if the still path invoked it.
    let mut forbidden=Fixture::new();forbidden.fail=true;
    let failure=error(unsafe {tt_digital_process(host.0,&c,pixels.as_mut_ptr(),pixels.len(),Some(provide),(&mut forbidden as *mut Fixture).cast())}).unwrap();
    assert!(failure.contains("replay exceeds per-call limit"),"{failure}");
    assert_eq!(pixels,original,"failed processing must not commit partially rendered pixels");
    assert!(forbidden.requested.is_empty(),"still history must not call an external video provider");
    // A small seek after the budget abort must be the same as a clean session.
    c.frame_index=7;
    ok(unsafe {tt_digital_process(host.0,&c,pixels.as_mut_ptr(),pixels.len(),Some(provide),(&mut forbidden as *mut Fixture).cast())});
    let fresh=OwnedSession::new(configuration);let mut expected=original;
    ok(unsafe {tt_digital_process(fresh.0,&c,expected.as_mut_ptr(),expected.len(),None,std::ptr::null_mut())});
    assert_eq!(pixels,expected);
    assert!(forbidden.requested.is_empty());
}
