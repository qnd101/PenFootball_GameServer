# 서버 측 로직 구현
게임 전체 정보를 담고 있는 singleton 서비스를 만들고
Hub에선 이벤트를 받아서 정보를 수정
Timer서비스에서는 HubContext와 정보를 기반으로 각 프레임을 프론트엔드에 전송

Game 객체 제작. 두 플레이어 사이의 게임을 나타냄. 각 플레이어의 상태를 지니고 그 상태를 어떻게 업데이트할지
외부로부터의 이벤트 정보는 EventQueue를 통해서 받음. 

Game 객체는 GetFrame()을 통해 json으로 변환 가능. 플레이어 뒤집기 기능도 필요

# Implementation
게임 로직은 웹 로직과는 완전 무관하게 진행
Game은 Player1, Player2, Ball, Obstacle 및 게임 로직을 구현한 Update 함수, 이벤트를 받아들이는 EventQueue를 제공.
그리고 다양한 이벤트 상황에 대해 외부에 eventhandler 제공

Update 실행 시 EventQueue를 해체. GameEvent는 자신이 처리하고 KeyEvent는 플레이어에게 넘김. 

# 계정 정보 관련
닉네임 : 고유정보 (key), string 형식
비밀번호: string 형식
레이팅 : int 형태의 숫자
통계 정보들 (일단 생략)

간략한 소개글? 


