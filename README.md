# Addressable_Custom
유니티 어드레서블 빌드까지의 과정을 자동화

재귀 탐색으로 폴더 내부를 검사해 리소스를 Addressable 화 하여 저장한다.
Addressable 리소스들을 폴더 단위로 그룹화하여 그룹명을 지정하고 리스트화 한다.
리스트화 된 리소스들을 빌드한다.


### [2023.10.26] 수정

1. 하위폴더들을 재귀적으로 탐색할 필요가 굳이 없다는 생각이 들었다. -> 그룹이 많아지고, 그룹이 많아지는 것이 다른 악영향을 미칠 수 있다. (네트워크가 좋지 않은 동남아 권에서 다운로드 이슈 등)

2. 리소스 폴더 하위의 첫 번째 하위 폴더 리스트를 가져와 각 폴더를 하나의 그룹으로 치환한다. -> 그룹 수를 줄여 관리를 쉽게 하고, 실제로는 자유롭게 하위폴더를 만들지만, 어드레서블에서는 상위 폴더 하위는 그룹화 하지 않음.

3. 그룹명으로 엔트리에 라벨을 붙힌다.
