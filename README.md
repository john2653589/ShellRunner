# ShellRunner
Version 1.0.1
From Rugal Tu
 
 #### 步驟
1. 執行專案 (ShellRunner)
2. 輸入執行指令
	- #open => 開啟檔案
	- #back => 回到原預設路徑目錄
	- #ssh => 執行ssh連線
	- #scp => 檔案傳送
	- #run => 執行檔案 (參考範例)

#### 範例 #run
###### 專案執行ShellRunner輸入指令
```
#run test.txt -@port 2210
```

###### test.txt 文件內容
```
路徑:C:\Users\Axel\Desktop\Github\ShellRunner\ShellRunner\bin\Debug\net6.0\test.txt

#ssh <UserName>@<Ip/Domain> -p @port -#p <Password>
sudo su -
cd /home
ls
```

#### 備註說明
- PowerShell、Cmd指令都可以執行

