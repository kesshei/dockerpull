# dockerpull
一个本地的可以配置代理的本地拉取镜像工具，主要是可以配置代理，方便使用

下载后，在命令处生成 tar文件，然后，使用 

docker load -i xxx.tar 即可导入镜像了。



## 示例
dockerpull elasticsearch:8.16.4 --proxy http://127.0.0.1:1080

### 扩展参数
--output 输出目录 （默认当前目录下）
--proxy 代理地址
--arch  架构类型，默认为 linux/amd64


### 安装

windows 请添加命令到 环境变量中 Path中。

linux  下
复制到 /usr/local/bin/dockerpull 目录 
然后使用 chmod + x /usr/local/bin/dockerpull 添加执行权限



### 主要参考
```
https://distribution.github.io/distribution/spec/api/
https://github.com/topcss/docker-pull-tar
```
